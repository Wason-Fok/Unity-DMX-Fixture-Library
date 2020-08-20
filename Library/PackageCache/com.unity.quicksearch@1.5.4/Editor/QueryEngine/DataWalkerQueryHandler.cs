using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;

namespace Unity.QuickSearch
{
    internal class DataWalkerQueryHandler<TData> : IQueryHandler<TData, Func<TData, bool>, IEnumerator<TData>>
    {
        private QueryEngine<TData> m_Engine;

        public Func<TData, bool> predicate { get; private set; }

        public void Initialize(QueryEngine<TData> engine, QueryGraph graph)
        {
            m_Engine = engine;
            if (graph != null)
                predicate = BuildFunctionFromNode(graph.root);
        }

        public IEnumerable<TData> Eval(Func<TData, bool> handler, IEnumerator<TData> payload)
        {
            while (payload.MoveNext())
            {
                if (handler(payload.Current))
                    yield return payload.Current;
            }
        }

        private Func<TData, bool> BuildFunctionFromNode(IQueryNode node)
        {
            switch (node.type)
            {
                case QueryNodeType.And:
                {
                    Assert.IsFalse(node.leaf, "And node cannot be leaf.");
                    var leftFunc = BuildFunctionFromNode(node.children[0]);
                    var rightFunc = BuildFunctionFromNode(node.children[1]);
                    return o => leftFunc(o) && rightFunc(o);
                }
                case QueryNodeType.Or:
                {
                    Assert.IsFalse(node.leaf, "Or node cannot be leaf.");
                    var leftFunc = BuildFunctionFromNode(node.children[0]);
                    var rightFunc = BuildFunctionFromNode(node.children[1]);
                    return o => leftFunc(o) || rightFunc(o);
                }
                case QueryNodeType.Not:
                {
                    Assert.IsFalse(node.leaf, "Not node cannot be leaf.");
                    var childFunc = BuildFunctionFromNode(node.children[0]);
                    return o => !childFunc(o);
                }
                case QueryNodeType.Filter:
                {
                    var filterNode = node as FilterNode;
                    var filterOperation = filterNode?.filterOperation as BaseFilterOperation<TData>;
                    Assert.IsNotNull(filterNode);
                    Assert.IsNotNull(filterOperation);
                    return o => filterOperation.Match(o);
                }
                case QueryNodeType.Search:
                {
                    if (m_Engine.searchDataCallback == null)
                        return o => false;
                    var searchNode = node as SearchNode;
                    Assert.IsNotNull(searchNode);
                    Func<string, bool> matchWordFunc;
                    if (searchNode.exact)
                        matchWordFunc = s => s.Equals(searchNode.searchValue, m_Engine.globalStringComparison);
                    else
                        matchWordFunc = s => s.IndexOf(searchNode.searchValue, m_Engine.globalStringComparison) >= 0;
                    return o => m_Engine.searchDataCallback(o).Any(data => matchWordFunc(data));
                }
            }

            return o => false;
        }
    }
}
