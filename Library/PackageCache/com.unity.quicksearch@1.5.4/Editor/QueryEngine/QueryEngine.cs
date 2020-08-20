using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using NodesToStringPosition = System.Collections.Generic.Dictionary<Unity.QuickSearch.IQueryNode, System.Tuple<int,int>>;

namespace Unity.QuickSearch
{
    internal interface IQueryHandler<TData, in THandler, in TPayload> where THandler : Delegate
    {
        IEnumerable<TData> Eval(THandler handler, TPayload payload);
        void Initialize(QueryEngine<TData> engine, QueryGraph graph);
    }

    internal interface IParseResult
    {
        bool success { get; }
    }

    internal readonly struct TypeParser
    {
        public readonly Type type;
        public readonly Delegate parser;

        public TypeParser(Type type, Delegate parser)
        {
            this.type = type;
            this.parser = parser;
        }
    }

    /// <summary>
    /// A ParseResult holds the result of a parsing operation.
    /// </summary>
    /// <typeparam name="T">Type of the result of the parsing operation.</typeparam>
    public readonly struct ParseResult<T> : IParseResult
    {
        /// <summary>
        /// Flag indicating if the parsing succeeded or not.
        /// </summary>
        public bool success { get; }

        /// <summary>
        /// Actual result of the parsing.
        /// </summary>
        public readonly T parsedValue;

        /// <summary>
        /// Create a ParseResult.
        /// </summary>
        /// <param name="success">Flag indicating if the parsing succeeded or not.</param>
        /// <param name="value">Actual result of the parsing.</param>
        public ParseResult(bool success, T value)
        {
            this.success = success;
            this.parsedValue = value;
        }
    }

    /// <summary>
    /// A QueryError holds the definition of a query parsing error.
    /// </summary>
    public class QueryError
    {
        /// <summary> Index where the error happened. </summary>
        public int index { get; }

        /// <summary> Length of the block that was being parsed. </summary>
        public int length { get; }

        /// <summary> Reason why the parsing failed. </summary>
        public string reason { get; }

        /// <summary>
        /// Construct a new QueryError with a default length of 1.
        /// </summary>
        /// <param name="index">Index where the error happened.</param>
        /// <param name="reason">Reason why the parsing failed.</param>
        public QueryError(int index, string reason)
        {
            this.index = index;
            this.reason = reason;
            length = 1;
        }

        /// <summary>
        /// Construct a new QueryError.
        /// </summary>
        /// <param name="index">Index where the error happened.</param>
        /// <param name="length">Length of the block that was being parsed.</param>
        /// <param name="reason">Reason why the parsing failed.</param>
        public QueryError(int index, int length, string reason)
        {
            this.index = index;
            this.reason = reason;
            this.length = length;
        }
    }

    /// <summary>
    /// A Query defines an operation that can be used to filter a data set.
    /// </summary>
    /// <typeparam name="TData">The filtered data type.</typeparam>
    /// <typeparam name="THandler">The type of the function called by the QueryHandler when walking its elements.</typeparam>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    public class Query<TData, THandler, TPayload>
        where THandler : Delegate
    {
        /// <summary>
        /// The engine that created this query.
        /// </summary>
        protected QueryEngine<TData> m_Engine;

        /// <summary> Indicates if the query is valid or not. </summary>
        public bool valid => errors.Count == 0 && graph != null;

        /// <summary> List of QueryErrors. </summary>
        public List<QueryError> errors { get; } = new List<QueryError>();

        internal IQueryHandler<TData, THandler, TPayload> graphHandler { get; set; }

        internal QueryGraph graph { get; }

        internal Query(QueryGraph graph, List<QueryError> errors, QueryEngine<TData> engine, IQueryHandler<TData, THandler, TPayload> graphHandler)
        {
            this.graph = graph;
            this.errors.AddRange(errors);
            m_Engine = engine;

            if (valid)
            {
                this.graphHandler = graphHandler;
                graphHandler.Initialize(engine, graph);
            }
        }

        internal IEnumerable<TData> Eval(THandler handler, TPayload payload)
        {
            if (!valid)
                return null;
            return graphHandler.Eval(handler, payload);
        }

        /// <summary>
        /// Optimize the query by optimizing the underlying filtering graph.
        /// </summary>
        /// <param name="propagateNotToLeaves">Propagate "Not" operations to leaves, so only leaves can have "Not" operations as parents.</param>
        /// <param name="swapNotToRightHandSide">Swaps "Not" operations to the right hand side of combining operations (i.e. "And", "Or"). Useful if a "Not" operation is slow.</param>
        public void Optimize(bool propagateNotToLeaves, bool swapNotToRightHandSide)
        {
            graph?.Optimize(propagateNotToLeaves, swapNotToRightHandSide);
        }
    }

    /// <summary>
    /// A Query defines an operation that can be used to filter a data set.
    /// </summary>
    /// <typeparam name="T">The filtered data type.</typeparam>
    public class Query<T> : Query<T, Func<T, bool>, IEnumerator<T>>
    {

        internal Query(QueryGraph graph, List<QueryError> errors, QueryEngine<T> engine)
            : base(graph, errors, engine, new DataWalkerQueryHandler<T>())
        {}

        /// <summary>
        /// Apply the filtering on an IEnumerable data set.
        /// </summary>
        /// <param name="data">The data to filter</param>
        /// <returns>A filtered IEnumerable.</returns>
        public IEnumerable<T> Apply(IEnumerable<T> data)
        {
            if (!valid)
                return data;
            return graphHandler.Eval(((DataWalkerQueryHandler<T>)graphHandler).predicate, data.GetEnumerator());
        }

        /// <summary>
        /// Apply the filtering on an IEnumerator.
        /// </summary>
        /// <param name="data">The data to filter</param>
        /// <returns>A filtered IEnumerable.</returns>
        public IEnumerable<T> Apply(IEnumerator<T> data)
        {
            if (!valid)
                return data.ToIEnumerable();
            return graphHandler.Eval(((DataWalkerQueryHandler<T>)graphHandler).predicate, data);
        }
    }

    internal static class EnumerableExtensions
    {
        public static IEnumerable<T> ToIEnumerable<T>(this IEnumerator<T> enumerator)
        {
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }
    }

    internal sealed class QueryEngineImpl<TData>
    {
        private List<IFilter> m_Filters = new List<IFilter>();
        private Func<TData, string, string, string, bool> m_DefaultFilterHandler;
        private Func<TData, string, string, string, string, bool> m_DefaultParamFilterHandler;
        private Dictionary<string, FilterOperator> m_FilterOperators = new Dictionary<string, FilterOperator>();
        private List<TypeParser> m_TypeParsers = new List<TypeParser>();

        // To match a regex at a specific index, use \\G and Match(input, startIndex)
        private static readonly Regex k_PhraseRx = new Regex("\\G!?\\\".*?\\\"");
        private static readonly Regex k_GroupStartRx = new Regex("\\G\\(");
        private Regex m_FilterRx = new Regex("\\G([\\w]+)([:><=!]+)(\\\".*?\\\"|[\\S]+)");
        private static readonly Regex k_WordRx = new Regex("\\G!?[\\w]+");
        private static readonly Regex k_EmptySpaceRx = new Regex("\\G\\s+");
        private static readonly Regex k_CombiningTokenRx = new Regex("(\\Gand\\b)|(\\Gor\\b)|(\\Gnot\\b)|(\\G-)");

        private delegate int TokenConsumer(string text, int tokenIndexStart, Match regexMatch, List<IQueryNode> nodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition);

        private List<Tuple<Regex, TokenConsumer>> m_TokenConsumers;

        public Func<TData, IEnumerable<string>> searchDataCallback { get; private set; }

        public bool validateFilters { get; set; }

        public StringComparison globalStringComparison { get; set; } = StringComparison.OrdinalIgnoreCase;

        public QueryEngineImpl()
        {
            // Default operators
            AddOperator(":", false)
                .AddHandler((object ev, object fv, StringComparison sc) => ev.ToString().IndexOf(fv.ToString(), sc) >= 0)
                .AddHandler((string ev, string fv, StringComparison sc) => ev.IndexOf(fv, sc) >= 0);
            AddOperator("=", false)
                .AddHandler((object ev, object fv) => ev.Equals(fv))
                .AddHandler((int ev, int fv) => ev == fv)
                .AddHandler((float ev, float fv) => Math.Abs(ev - fv) < Mathf.Epsilon)
                .AddHandler((bool ev, bool fv) => ev == fv)
                .AddHandler((string ev, string fv, StringComparison sc) => string.Equals(ev, fv, sc));
            AddOperator("!=", false)
                .AddHandler((object ev, object fv) => !ev.Equals(fv))
                .AddHandler((int ev, int fv) => ev != fv)
                .AddHandler((float ev, float fv) => Math.Abs(ev - fv) >= Mathf.Epsilon)
                .AddHandler((bool ev, bool fv) => ev != fv)
                .AddHandler((string ev, string fv, StringComparison sc) => !string.Equals(ev, fv, sc));
            AddOperator("<", false)
                .AddHandler((object ev, object fv) => Comparer<object>.Default.Compare(ev, fv) < 0)
                .AddHandler((int ev, int fv) => ev < fv)
                .AddHandler((float ev, float fv) => ev < fv)
                .AddHandler((string ev, string fv, StringComparison sc) => string.Compare(ev, fv, sc) < 0);
            AddOperator(">", false)
                .AddHandler((object ev, object fv) => Comparer<object>.Default.Compare(ev, fv) > 0)
                .AddHandler((int ev, int fv) => ev > fv)
                .AddHandler((float ev, float fv) => ev > fv)
                .AddHandler((string ev, string fv, StringComparison sc) => string.Compare(ev, fv, sc) > 0);
            AddOperator("<=", false)
                .AddHandler((object ev, object fv) => Comparer<object>.Default.Compare(ev, fv) <= 0)
                .AddHandler((int ev, int fv) => ev <= fv)
                .AddHandler((float ev, float fv) => ev <= fv)
                .AddHandler((string ev, string fv, StringComparison sc) => string.Compare(ev, fv, sc) <= 0);
            AddOperator(">=", false)
                .AddHandler((object ev, object fv) => Comparer<object>.Default.Compare(ev, fv) >= 0)
                .AddHandler((int ev, int fv) => ev >= fv)
                .AddHandler((float ev, float fv) => ev >= fv)
                .AddHandler((string ev, string fv, StringComparison sc) => string.Compare(ev, fv, sc) >= 0);

            BuildFilterRegex();
        }

        public void AddFilter(IFilter filter)
        {
            m_Filters.Add(filter);
        }

        public FilterOperator AddOperator(string op)
        {
            return AddOperator(op, true);
        }

        private FilterOperator AddOperator(string op, bool rebuildFilterRegex)
        {
            if (m_FilterOperators.ContainsKey(op))
                return m_FilterOperators[op];
            var filterOperator = new FilterOperator(op);
            m_FilterOperators.Add(op, filterOperator);
            if (rebuildFilterRegex)
                BuildFilterRegex();
            return filterOperator;
        }

        public FilterOperator GetOperator(string op)
        {
            return m_FilterOperators.ContainsKey(op) ? m_FilterOperators[op] : null;
        }

        public void AddOperatorHandler<TLhs, TRhs>(string op, Func<TLhs, TRhs, bool> handler)
        {
            if (!m_FilterOperators.ContainsKey(op))
                return;
            m_FilterOperators[op].AddHandler(handler);
        }

        public void SetDefaultFilter(Func<TData, string, string, string, bool> handler)
        {
            m_DefaultFilterHandler = handler;
        }

        public void SetDefaultParamFilter(Func<TData, string, string, string, string, bool> handler)
        {
            m_DefaultParamFilterHandler = handler;
        }

        public void SetSearchDataCallback(Func<TData, IEnumerable<string>> getSearchDataCallback)
        {
            searchDataCallback = getSearchDataCallback;
        }

        public void AddTypeParser<TFilterConstant>(Func<string, ParseResult<TFilterConstant>> parser)
        {
            m_TypeParsers.Add(new TypeParser(typeof(TFilterConstant), parser));
        }

        internal IQueryNode BuildExpressionGraph(string text, int stringIndex, int endIndex, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            var expressionNodes = new List<IQueryNode>();
            var index = stringIndex;
            while (index < endIndex)
            {
                var matched = false;
                foreach (var tokenConsumer in m_TokenConsumers)
                {
                    var match = tokenConsumer.Item1.Match(text, index, endIndex - index);
                    if (match.Success)
                    {
                        var consumed = tokenConsumer.Item2(text, index, match, expressionNodes, errors, nodesToStringPosition);
                        if (consumed == -1)
                        {
                            return null;
                        }
                        index += consumed;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    errors.Add(new QueryError(index, $"Error parsing string. No token could be deduced at {index}"));
                    return null;
                }
            }

            InsertAndIfNecessary(expressionNodes, nodesToStringPosition);
            var rootNode = CombineNodesToTree(expressionNodes, errors, nodesToStringPosition);
            ValidateGraph(rootNode, errors, nodesToStringPosition);
            return rootNode;
        }

        private static int ConsumeEmpty(string text, int startIndex, Match match, List<IQueryNode> nodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            return match.Length;
        }

        private static int ConsumeCombiningToken(string text, int startIndex, Match match, List<IQueryNode> nodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            if (IsCombiningToken(match.Value))
            {
                var newNode = CreateCombiningNode(match.Value);
                nodes.Add(newNode);
                nodesToStringPosition.Add(newNode, new Tuple<int, int>(startIndex, match.Length));
                return match.Length;
            }

            return -1;
        }

        private int ConsumeFilter(string text, int startIndex, Match match, List<IQueryNode> nodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            if (IsFilterToken(match.Value))
            {
                var node = CreateFilterToken(match.Value, startIndex, errors);
                if (node != null)
                {
                    nodesToStringPosition.Add(node, new Tuple<int, int>(startIndex, match.Length));
                    nodes.Add(node);
                }

                return match.Length;
            }

            return -1;
        }

        private int ConsumeWords(string text, int startIndex, Match match, List<IQueryNode> nodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            if (validateFilters && searchDataCallback == null)
            {
                errors.Add(new QueryError(startIndex, match.Length, "Cannot use a search word without setting the search data callback."));
                return -1;
            }

            if (IsPhraseToken(match.Value) || IsWordToken(match.Value))
            {
                var node = CreateWordExpressionNode(match.Value);
                if (node != null)
                {
                    nodesToStringPosition.Add(node, new Tuple<int, int>(startIndex, match.Length));
                    nodes.Add(node);
                }

                return match.Length;
            }

            return -1;
        }

        private int ConsumeGroup(string text, int groupStartIndex, Match match, List<IQueryNode> nodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            if (groupStartIndex < 0 || groupStartIndex >= text.Length)
            {
                errors.Add(new QueryError(0, $"A group should have been found but index was {groupStartIndex}"));
                return -1;
            }

            var charConsumed = 0;

            var parenthesisCounter = 1;
            var groupEndIndex = groupStartIndex + 1;
            for (; groupEndIndex < text.Length && parenthesisCounter > 0; ++groupEndIndex)
            {
                if (text[groupEndIndex] == '(')
                    ++parenthesisCounter;
                else if (text[groupEndIndex] == ')')
                    --parenthesisCounter;
            }

            // Because of the final ++groupEndIndex, decrement the index
            --groupEndIndex;

            if (parenthesisCounter != 0)
            {
                errors.Add(new QueryError(groupStartIndex, $"Unbalanced parenthesis"));
                return -1;
            }

            charConsumed = groupEndIndex - groupStartIndex + 1;

            var groupNode = BuildExpressionGraph(text, groupStartIndex + 1, groupEndIndex, errors, nodesToStringPosition);
            if (groupNode != null)
                nodes.Add(groupNode);

            return charConsumed;
        }

        private static void InsertAndIfNecessary(List<IQueryNode> nodes, NodesToStringPosition nodesToStringPosition)
        {
            if (nodes.Count <= 1)
                return;

            for (var i = 0; i < nodes.Count - 1; ++i)
            {
                if (nodes[i] is CombinedNode cn && cn.leaf)
                    continue;
                if (nodes[i + 1] is CombinedNode nextCn && nextCn.leaf && nextCn.type != QueryNodeType.Not)
                    continue;

                var andNode = new AndNode();
                var previousNodePosition = nodesToStringPosition[nodes[i]];
                var nextNodePosition = nodesToStringPosition[nodes[i + 1]];
                var startPosition = previousNodePosition.Item1 + previousNodePosition.Item2;
                var length = nextNodePosition.Item1 - startPosition;
                nodesToStringPosition.Add(andNode, new Tuple<int, int>(startPosition, length));
                nodes.Insert(i + 1, andNode);
                // Skip this new node
                ++i;
            }
        }

        private static bool IsCombiningToken(string token)
        {
            return token == "and" || token == "or" || token == "not" || token == "-";
        }

        private bool IsFilterToken(string token)
        {
            return m_FilterRx.IsMatch(token);
        }

        private static bool IsPhraseToken(string token)
        {
            return k_PhraseRx.IsMatch(token);
        }

        private static bool IsWordToken(string token)
        {
            return k_WordRx.IsMatch(token);
        }

        private static IQueryNode CreateCombiningNode(string token)
        {
            if (token == "and")
                return new AndNode();
            if (token == "or")
                return new OrNode();
            if (token == "not" || token == "-")
                return new NotNode();
            return null;
        }

        private IQueryNode CreateFilterToken(string token, int index, List<QueryError> errors)
        {
            var match = m_FilterRx.Match(token);
            if (match.Groups.Count != 5)
            {
                errors.Add(new QueryError(index, token.Length, $"Could not parse filter block \"{token}\"."));
                return null;
            }

            var filterType = match.Groups[1].Value;
            var filterParam = match.Groups[2].Value;
            var filterOperator = match.Groups[3].Value;
            var filterValue = match.Groups[4].Value;

            if (!string.IsNullOrEmpty(filterParam))
            {
                // Trim () around the group
                filterParam = filterParam.Trim('(', ')');
            }

            var filter = m_Filters.FirstOrDefault(f => f.token == filterType);
            if (filter == null)
            {
                if (m_DefaultFilterHandler == null && validateFilters)
                {
                    errors.Add(new QueryError(index + match.Groups[1].Index, filterType.Length, $"Unknown filter type \"{filterType}\"."));
                    return null;
                }
                if (string.IsNullOrEmpty(filterParam))
                    filter = new DefaultFilter<TData>(filterType, m_DefaultFilterHandler ?? ((o, s, fo, value) => false) );
                else
                    filter = new DefaultParamFilter<TData>(filterType, m_DefaultParamFilterHandler ?? ((o, s, param, fo, value) => false));
            }

            var op = m_FilterOperators.FirstOrDefault(fo => fo.Value.token == filterOperator).Value;
            if (op == null)
            {
                errors.Add(new QueryError(index + match.Groups[2].Index, filterOperator.Length, $"Unknown filter operator \"{filterOperator}\"."));
                return null;
            }

            if (filter.supportedFilters.Any() && !filter.supportedFilters.Any(filterOp => filterOp.Equals(op.token)))
            {
                errors.Add(new QueryError(index + match.Groups[2].Index, filterOperator.Length, $"The filter \"{op.token}\" is not supported for this filter."));
                return null;
            }

            if (IsPhraseToken(filterValue))
                filterValue = filterValue.Trim('"');

            var filterValueType = filter.type;
            var foundValueType = false;
            IParseResult parseResult = null;
            var handlerTypes = op.handlers.Keys.Where(key => key.leftHandSideType == filter.type).ToList();
            // Keep object handlers at the end.
            if (filter.type != typeof(object))
                handlerTypes.AddRange(op.handlers.Keys.Where(key => key.leftHandSideType == typeof(object)));
            foreach (var opHandlerTypes in handlerTypes)
            {
                var rhsType = opHandlerTypes.rightHandSideType;
                parseResult = GenerateParseResultForType(filterValue, rhsType);
                if (parseResult.success)
                {
                    filterValueType = rhsType;
                    foundValueType = true;
                    break;
                }
            }

            if (!foundValueType)
            {
                // Try one last time with the type of the filter instead
                parseResult = GenerateParseResultForType(filterValue, filter.type);
                if (!parseResult.success)
                {
                    errors.Add(new QueryError(index + match.Groups[3].Index, filterValue.Length, $"The value {filterValue} could not be converted to any of the supported handler types."));
                    return null;
                }
            }

            IFilterOperationGenerator<TData> filterOperationGenerator;

            if (!filter.paramFilter)
            {
                Type type;
                if (filter.resolver)
                    type = typeof(FilterResolverOperationGenerator<,>).MakeGenericType(typeof(TData), filter.type);
                else
                    type = typeof(FilterOperationGenerator<,,>).MakeGenericType(typeof(TData), filter.type, filterValueType);
                filterOperationGenerator = (IFilterOperationGenerator<TData>)Activator.CreateInstance(type, filter, op, filterValue, parseResult);
            }
            else
            {
                Type type;
                if (filter.resolver)
                    type = typeof(FilterResolverOperationGenerator<,,>).MakeGenericType(typeof(TData), filter.paramType, filter.type);
                else
                    type = typeof(FilterOperationGenerator<,,,>).MakeGenericType(typeof(TData), filter.paramType, filter.type, filterValueType);
                filterOperationGenerator = (IFilterOperationGenerator<TData>)Activator.CreateInstance(type, filter, op, filterValue, filterParam, parseResult);
            }

            return new FilterNode(filterOperationGenerator.GenerateOperation(index + match.Groups[2].Index, errors, this));
        }

        private static IQueryNode CreateWordExpressionNode(string token)
        {
            var isExact = token.StartsWith("!");
            if (isExact)
                token = token.Remove(0, 1);
            if (IsPhraseToken(token))
                token = token.Trim('"');

            return new SearchNode(token, isExact);
        }

        private static IQueryNode CombineNodesToTree(List<IQueryNode> expressionNodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            var count = expressionNodes.Count;
            if (count == 0)
                return null;

            CombineNotNodes(expressionNodes, errors, nodesToStringPosition);
            CombineAndOrNodes(QueryNodeType.And, expressionNodes, errors, nodesToStringPosition);
            CombineAndOrNodes(QueryNodeType.Or, expressionNodes, errors, nodesToStringPosition);

            return expressionNodes[0];
        }

        private static void CombineNotNodes(List<IQueryNode> expressionNodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            var count = expressionNodes.Count;
            if (count == 0)
                return;

            for (var i = count - 1; i >= 0; --i)
            {
                var currentNode = expressionNodes[i];
                var nextNode = i < count - 1 ? expressionNodes[i + 1] : null;
                if (currentNode is CombinedNode combinedNode)
                {
                    if (combinedNode.leaf)
                    {
                        var (startIndex, length) = nodesToStringPosition[currentNode];
                        if (currentNode.type == QueryNodeType.Not)
                        {
                            if (nextNode == null)
                            {
                                errors.Add(new QueryError(startIndex + length, $"Missing operand to combine with node {currentNode.type}."));
                            }
                            else
                            {
                                combinedNode.AddNode(nextNode);
                                expressionNodes.RemoveAt(i + 1);
                            }
                        }
                    }
                }
            }
        }

        private static void CombineAndOrNodes(QueryNodeType nodeType, List<IQueryNode> expressionNodes, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            var count = expressionNodes.Count;
            if (count == 0)
                return;

            for (var i = count - 1; i >= 0; --i)
            {
                var currentNode = expressionNodes[i];
                var nextNode = i < count - 1 ? expressionNodes[i + 1] : null;
                var previousNode = i > 0 ? expressionNodes[i - 1] : null;
                if (currentNode is CombinedNode combinedNode)
                {
                    if (combinedNode.leaf)
                    {
                        var (startIndex, length) = nodesToStringPosition[currentNode];
                        if (currentNode.type == nodeType)
                        {
                            if (previousNode == null)
                            {
                                errors.Add(new QueryError(startIndex + length, $"Missing left-hand operand to combine with node {currentNode.type}."));
                            }
                            else
                            {
                                combinedNode.AddNode(previousNode);
                                expressionNodes.RemoveAt(i - 1);
                                // Update current index
                                --i;
                            }

                            if (nextNode == null)
                            {
                                errors.Add(new QueryError(startIndex + length, $"Missing right-hand operand to combine with node {currentNode.type}."));
                            }
                            else
                            {
                                combinedNode.AddNode(nextNode);
                                expressionNodes.RemoveAt(i + 1);
                            }
                        }
                    }
                }
            }
        }

        private static void ValidateGraph(IQueryNode root, List<QueryError> errors, NodesToStringPosition nodesToStringPosition)
        {
            if (root == null)
            {
                errors.Add(new QueryError(0, "Encountered a null node."));
                return;
            }
            var (position, length) = nodesToStringPosition[root];
            if (root is CombinedNode cn)
            {
                if (root.leaf)
                {
                    errors.Add(new QueryError(position, length, $"Node {root.type} is a leaf."));
                    return;
                }

                if (root.type == QueryNodeType.Not && root.children.Count != 1)
                {
                    errors.Add(new QueryError(position, length, $"Node {root.type} should have a child."));
                }
                else if (root.type != QueryNodeType.Not && root.children.Count != 2)
                {
                    errors.Add(new QueryError(position, length, $"Node {root.type} should have 2 children."));
                }

                foreach (var child in root.children)
                {
                    ValidateGraph(child, errors, nodesToStringPosition);
                }
            }
        }

        private void BuildFilterRegex()
        {
            var sortedOperators = m_FilterOperators.Keys.Select(Regex.Escape).ToList();
            sortedOperators.Sort((s, s1) => s1.Length.CompareTo(s.Length));
            var filterRx = $"\\G([\\w]+)(\\([^\\(\\)]+\\))?({string.Join("|", sortedOperators)}+)(\\\".*?\\\"|[\\S]+)";
            m_FilterRx = new Regex(filterRx, RegexOptions.Compiled);

            m_TokenConsumers = new List<Tuple<Regex, TokenConsumer>>
            {
                Tuple.Create<Regex, TokenConsumer>(k_EmptySpaceRx, ConsumeEmpty),
                Tuple.Create<Regex, TokenConsumer>(k_CombiningTokenRx, ConsumeCombiningToken),
                Tuple.Create<Regex, TokenConsumer>(k_GroupStartRx, ConsumeGroup),
                Tuple.Create<Regex, TokenConsumer>(m_FilterRx, ConsumeFilter),
                Tuple.Create<Regex, TokenConsumer>(k_PhraseRx, ConsumeWords),
                Tuple.Create<Regex, TokenConsumer>(k_WordRx, ConsumeWords),
            };
        }

        private IParseResult GenerateParseResultForType(string value, Type type)
        {
            var thisClassType = typeof(QueryEngineImpl<TData>);
            var method = thisClassType.GetMethod("ParseData", BindingFlags.NonPublic | BindingFlags.Instance);
            var typedMethod = method.MakeGenericMethod(type);
            return typedMethod.Invoke(this, new object[] { value }) as IParseResult;
        }

        private ParseResult<T> ParseData<T>(string value)
        {
            var parserIndex = m_TypeParsers.FindIndex(typeParser => typeParser.type == typeof(T));
            if (parserIndex > -1)
            {
                var parser = m_TypeParsers[parserIndex];
                if (parser.parser.DynamicInvoke(value) is ParseResult<T> pr)
                {
                    return pr;
                }
            }
            else
            {
                if (Utils.TryConvertValue(value, out T parsedValue))
                {
                    return new ParseResult<T>(true, parsedValue);
                }
            }

            return new ParseResult<T>(false, default);
        }
    }

    /// <summary>
    /// A QueryEngine defines how to build a query from an input string.
    /// It can be customized to support custom filters and operators.
    /// </summary>
    /// <typeparam name="TData">The filtered data type.</typeparam>
    public class QueryEngine<TData>
    {
        private QueryEngineImpl<TData> m_Impl;

        /// <summary>
        /// Get of set if the engine must validate filters when parsing the query. Defaults to true.
        /// </summary>
        public bool validateFilters
        {
            get => m_Impl.validateFilters;
            set => m_Impl.validateFilters = value;
        }

        /// <summary>
        /// Global string comparison options for word matching and filter handling (if not overridden by filter).
        /// </summary>
        public StringComparison globalStringComparison => m_Impl.globalStringComparison;

        /// <summary>
        /// The callback used to get the data to match to the search words.
        /// </summary>
        public Func<TData, IEnumerable<string>> searchDataCallback => m_Impl.searchDataCallback;

        /// <summary>
        /// Construct a new QueryEngine.
        /// </summary>
        public QueryEngine()
        {
            m_Impl = new QueryEngineImpl<TData> { validateFilters = true };
        }

        /// <summary>
        /// Construct a new QueryEngine.
        /// </summary>
        /// <param name="validateFilters">Indicates if the engine must validate filters when parsing the query.</param>
        public QueryEngine(bool validateFilters)
        {
            m_Impl = new QueryEngineImpl<TData> { validateFilters = validateFilters };
        }

        /// <summary>
        /// Add a new custom filter.
        /// </summary>
        /// <typeparam name="TFilter">The type of the data that is compared by the filter.</typeparam>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter (i.e. "id" in "id>=2").</param>
        /// <param name="getDataFunc">Callback used to get the object that is used in the filter. Takes an object of type TData and returns an object of type TFilter.</param>
        /// <param name="supportedOperatorType">List of supported operator tokens. Null for all operators.</param>
        public void AddFilter<TFilter>(string token, Func<TData, TFilter> getDataFunc, string[] supportedOperatorType = null)
        {
            var filter = new Filter<TData, TFilter>(token, supportedOperatorType, getDataFunc);
            m_Impl.AddFilter(filter);
        }

        /// <summary>
        /// Add a new custom filter.
        /// </summary>
        /// <typeparam name="TFilter">The type of the data that is compared by the filter.</typeparam>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter (i.e. "id" in "id>=2").</param>
        /// <param name="getDataFunc">Callback used to get the object that is used in the filter. Takes an object of type TData and returns an object of type TFilter.</param>
        /// <param name="stringComparison">String comparison options.</param>
        /// <param name="supportedOperatorType">List of supported operator tokens. Null for all operators.</param>
        public void AddFilter<TFilter>(string token, Func<TData, TFilter> getDataFunc, StringComparison stringComparison, string[] supportedOperatorType = null)
        {
            var filter = new Filter<TData, TFilter>(token, supportedOperatorType, getDataFunc, stringComparison);
            m_Impl.AddFilter(filter);
        }

        /// <summary>
        /// Add a new custom filter function.
        /// </summary>
        /// <typeparam name="TParam">The type of the constant parameter passed to the function.</typeparam>
        /// <typeparam name="TFilter">The type of the data that is compared by the filter.</typeparam>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter (i.e. "id" in "id>=2").</param>
        /// <param name="getDataFunc">Callback used to get the object that is used in the filter. Takes an object of type TData and TParam, and returns an object of type TFilter.</param>
        /// <param name="supportedOperatorType">List of supported operator tokens. Null for all operators.</param>
        public void AddFilter<TParam, TFilter>(string token, Func<TData, TParam, TFilter> getDataFunc, string[] supportedOperatorType = null)
        {
            var filter = new Filter<TData, TParam, TFilter>(token, supportedOperatorType, getDataFunc);
            m_Impl.AddFilter(filter);
        }

        /// <summary>
        /// Add a new custom filter function.
        /// </summary>
        /// <typeparam name="TParam">The type of the constant parameter passed to the function.</typeparam>
        /// <typeparam name="TFilter">The type of the data that is compared by the filter.</typeparam>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter (i.e. "id" in "id>=2").</param>
        /// <param name="getDataFunc">Callback used to get the object that is used in the filter. Takes an object of type TData and TParam, and returns an object of type TFilter.</param>
        /// <param name="stringComparison">String comparison options.</param>
        /// <param name="supportedOperatorType">List of supported operator tokens. Null for all operators.</param>
        public void AddFilter<TParam, TFilter>(string token, Func<TData, TParam, TFilter> getDataFunc, StringComparison stringComparison, string[] supportedOperatorType = null)
        {
            var filter = new Filter<TData, TParam, TFilter>(token, supportedOperatorType, getDataFunc, stringComparison);
            m_Impl.AddFilter(filter);
        }

        /// <summary>
        /// Add a new custom filter function.
        /// </summary>
        /// <typeparam name="TParam">The type of the constant parameter passed to the function.</typeparam>
        /// <typeparam name="TFilter">The type of the data that is compared by the filter.</typeparam>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter (i.e. "id" in "id>=2").</param>
        /// <param name="getDataFunc">Callback used to get the object that is used in the filter. Takes an object of type TData and TParam, and returns an object of type TFilter.</param>
        /// <param name="parameterTransformer">Callback used to convert a string to the type TParam. Used when parsing the query to convert what is passed to the function into the correct format.</param>
        /// <param name="supportedOperatorType">List of supported operator tokens. Null for all operators.</param>
        public void AddFilter<TParam, TFilter>(string token, Func<TData, TParam, TFilter> getDataFunc, Func<string, TParam> parameterTransformer, string[] supportedOperatorType = null)
        {
            var filter = new Filter<TData, TParam, TFilter>(token, supportedOperatorType, getDataFunc, parameterTransformer);
            m_Impl.AddFilter(filter);
        }

        /// <summary>
        /// Add a new custom filter function.
        /// </summary>
        /// <typeparam name="TParam">The type of the constant parameter passed to the function.</typeparam>
        /// <typeparam name="TFilter">The type of the data that is compared by the filter.</typeparam>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter (i.e. "id" in "id>=2").</param>
        /// <param name="getDataFunc">Callback used to get the object that is used in the filter. Takes an object of type TData and TParam, and returns an object of type TFilter.</param>
        /// <param name="parameterTransformer">Callback used to convert a string to the type TParam. Used when parsing the query to convert what is passed to the function into the correct format.</param>
        /// <param name="stringComparison">String comparison options.</param>
        /// <param name="supportedOperatorType">List of supported operator tokens. Null for all operators.</param>
        public void AddFilter<TParam, TFilter>(string token, Func<TData, TParam, TFilter> getDataFunc, Func<string, TParam> parameterTransformer, StringComparison stringComparison, string[] supportedOperatorType = null)
        {
            var filter = new Filter<TData, TParam, TFilter>(token, supportedOperatorType, getDataFunc, parameterTransformer, stringComparison);
            m_Impl.AddFilter(filter);
        }

        /// <summary>
        /// Add a new custom filter with a custom resolver. Useful when you wish to handle all operators yourself.
        /// </summary>
        /// <typeparam name="TFilter">The type of the data that is compared by the filter.</typeparam>
        /// <param name="token">The identifier of the filter. Typically what precedes the operator in a filter (i.e. "id" in "id>=2").</param>
        /// <param name="filterResolver">Callback used to handle any operators for this filter. Takes an object of type TData, the operator token and the filter value, and returns a boolean indicating if the filter passed or not.</param>
        /// <param name="supportedOperatorType">List of supported operator tokens. Null for all operators.</param>
        public void AddFilter<TFilter>(string token, Func<TData, string, TFilter, bool> filterResolver, string[] supportedOperatorType = null)
        {
            var filter = new Filter<TData, TFilter>(token, supportedOperatorType, filterResolver);
            m_Impl.AddFilter(filter);
        }

        /// <summary>
        /// Add a custom filter operator.
        /// </summary>
        /// <param name="op">The operator identifier.</param>
        public void AddOperator(string op)
        {
            m_Impl.AddOperator(op);
        }

        /// <summary>
        /// Add a custom filter operator handler.
        /// </summary>
        /// <typeparam name="TFilterVariable">The operator's left hand side type. This is the type returned by a filter handler.</typeparam>
        /// <typeparam name="TFilterConstant">The operator's right hand side type.</typeparam>
        /// <param name="op">The filter operator.</param>
        /// <param name="handler">Callback to handle the operation. Takes a TFilterVariable (value returned by the filter handler, will vary for each element) and a TFilterConstant (right hand side value of the operator, which is constant), and returns a boolean indicating if the filter passes or not.</param>
        public void AddOperatorHandler<TFilterVariable, TFilterConstant>(string op, Func<TFilterVariable, TFilterConstant, bool> handler)
        {
            m_Impl.AddOperatorHandler(op, handler);
        }

        /// <summary>
        /// Add a type parser that parse a string and returns a custom type. Used
        /// by custom operator handlers.
        /// </summary>
        /// <typeparam name="TFilterConstant">The type of the parsed operand that is on the right hand side of the operator.</typeparam>
        /// <param name="parser">Callback used to determine if a string can be converted into TFilterConstant. Takes a string and returns a ParseResult object. This contains the success flag, and the actual converted value if it succeeded.</param>
        public void AddTypeParser<TFilterConstant>(Func<string, ParseResult<TFilterConstant>> parser)
        {
            m_Impl.AddTypeParser(parser);
        }

        /// <summary>
        /// Set the default filter handler for filters that were not registered.
        /// </summary>
        /// <param name="handler">Callback used to handle the filter. Takes an object of type TData, the filter identifier, the operator and the filter value, and returns a boolean indicating if the filter passed or not.</param>
        public void SetDefaultFilter(Func<TData, string, string, string, bool> handler)
        {
            m_Impl.SetDefaultFilter(handler);
        }

        /// <summary>
        /// Set the default filter handler for function filters that were not registered.
        /// </summary>
        /// <param name="handler">Callback used to handle the function filter. Takes an object of type TData, the filter identifier, the parameter, the operator and the filter value, and returns a boolean indicating if the filter passed or not.</param>
        public void SetDefaultParamFilter(Func<TData, string, string, string, string, bool> handler)
        {
            m_Impl.SetDefaultParamFilter(handler);
        }

        /// <summary>
        /// Set the callback to be used to fetch the data that will be matched against the search words.
        /// </summary>
        /// <param name="getSearchDataCallback">Callback used to get the data to be matched against the search words. Takes an object of type TData and return an IEnumerable of strings.</param>
        public void SetSearchDataCallback(Func<TData, IEnumerable<string>> getSearchDataCallback)
        {
            m_Impl.SetSearchDataCallback(getSearchDataCallback);
        }

        /// <summary>
        /// Set global string comparison options. Used for word matching and filter handling (unless overridden by filter).
        /// </summary>
        /// <param name="stringComparison">String comparison options.</param>
        public void SetGlobalStringComparisonOptions(StringComparison stringComparison)
        {
            m_Impl.globalStringComparison = stringComparison;
        }

        /// <summary>
        /// Parse a query string into a Query operation. This Query operation can then be used to filter any data set of type TData.
        /// </summary>
        /// <param name="text">The query input string.</param>
        /// <returns>Query operation of type TData.</returns>
        public Query<TData> Parse(string text)
        {
            var errors = new List<QueryError>();
            var nodesToStringPosition = new NodesToStringPosition();
            var graphRootNode = m_Impl.BuildExpressionGraph(text, 0, text.Length, errors, nodesToStringPosition);
            var graph = new QueryGraph(graphRootNode);
            return new Query<TData>(graph, errors, this);
        }

        internal Query<TData, THandler, TPayload> Parse<TGraphHandler, THandler, TPayload>(string text)
            where TGraphHandler : IQueryHandler<TData, THandler, TPayload>, new()
            where THandler : Delegate
        {
            var errors = new List<QueryError>();
            var nodesToStringPosition = new NodesToStringPosition();
            var graphRootNode = m_Impl.BuildExpressionGraph(text, 0, text.Length, errors, nodesToStringPosition);
            var graph = new QueryGraph(graphRootNode);
            return new Query<TData, THandler, TPayload>(graph, errors, this, new TGraphHandler());
        }
    }

    /// <summary>
    /// A QueryEngine defines how to build a query from an input string.
    /// It can be customized to support custom filters and operators.
    /// Default query engine of type object.
    /// </summary>
    public class QueryEngine : QueryEngine<object>
    {
        /// <summary>
        /// Construct a new QueryEngine.
        /// </summary>
        public QueryEngine()
        { }

        /// <summary>
        /// Construct a new QueryEngine.
        /// </summary>
        /// <param name="validateFilters">Indicates if the engine must validate filters when parsing the query.</param>
        public QueryEngine(bool validateFilters)
            : base(validateFilters)
        { }
    }
}
