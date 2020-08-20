using System.Collections.Generic;

namespace Unity.QuickSearch
{
    internal enum QueryNodeType
    {
        And,
        Or,
        Filter,
        Search,
        Not
    }

    internal interface IQueryNode
    {
        IQueryNode parent { get; set; }
        QueryNodeType type { get; }
        List<IQueryNode> children { get; }
        bool leaf { get; }
    }

    internal class FilterNode : IQueryNode
    {
        public IFilterOperation filterOperation;

        public IQueryNode parent { get; set; }
        public QueryNodeType type => QueryNodeType.Filter;
        public List<IQueryNode> children => new List<IQueryNode>();
        public bool leaf => true;

        public FilterNode(IFilterOperation operation)
        {
            filterOperation = operation;
        }
    }

    internal class SearchNode : IQueryNode
    {
        public bool exact { get; }
        public string searchValue { get; }

        public IQueryNode parent { get; set; }
        public QueryNodeType type => QueryNodeType.Search;
        public List<IQueryNode> children => new List<IQueryNode>();
        public bool leaf => true;

        public SearchNode(string searchValue, bool isExact)
        {
            this.searchValue = searchValue;
            exact = isExact;
        }
    }

    internal abstract class CombinedNode : IQueryNode
    {
        public IQueryNode parent { get; set; }
        public abstract QueryNodeType type { get; }
        public List<IQueryNode> children { get; }
        public bool leaf => children.Count == 0;

        protected CombinedNode()
        {
            children = new List<IQueryNode>();
        }

        public void AddNode(IQueryNode node)
        {
            children.Add(node);
            node.parent = this;
        }

        public void RemoveNode(IQueryNode node)
        {
            if (!children.Contains(node))
                return;

            children.Remove(node);
            if (node.parent == this)
                node.parent = null;
        }

        public void Clear()
        {
            foreach (var child in children)
            {
                if (child.parent == this)
                    child.parent = null;
            }
            children.Clear();
        }

        public abstract void SwapChildNodes();
    }

    internal class AndNode : CombinedNode
    {
        public override QueryNodeType type => QueryNodeType.And;

        public AndNode()
        { }

        public override void SwapChildNodes()
        {
            if (children.Count != 2)
                return;

            var tmp = children[0];
            children[0] = children[1];
            children[1] = tmp;
        }
    }

    internal class OrNode : CombinedNode
    {
        public override QueryNodeType type => QueryNodeType.Or;

        public OrNode()
        { }

        public override void SwapChildNodes()
        {
            if (children.Count != 2)
                return;

            var tmp = children[0];
            children[0] = children[1];
            children[1] = tmp;
        }
    }

    internal class NotNode : CombinedNode
    {
        public override QueryNodeType type => QueryNodeType.Not;

        public NotNode()
        { }

        public override void SwapChildNodes()
        { }
    }
}
