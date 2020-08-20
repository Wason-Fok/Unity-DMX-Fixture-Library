using System;
using System.Collections.Generic;
using System.Globalization;

namespace Unity.QuickSearch
{
    internal readonly struct FilterOperatorTypes
    {
        public readonly Type leftHandSideType;
        public readonly Type rightHandSideType;

        public FilterOperatorTypes(Type lhs, Type rhs)
        {
            leftHandSideType = lhs;
            rightHandSideType = rhs;
        }
    }

    internal class FilterOperator
    {
        public string token { get; }
        public Dictionary<FilterOperatorTypes, Delegate> handlers { get; }

        public FilterOperator(string token)
        {
            this.token = token;
            handlers = new Dictionary<FilterOperatorTypes, Delegate>();
        }

        public FilterOperator AddHandler<TLhs, TRhs>(Func<TLhs, TRhs, bool> handler)
        {
            return AddHandler((TLhs l, TRhs r, StringComparison sc) => handler(l, r));
        }

        public FilterOperator AddHandler<TLhs, TRhs>(Func<TLhs, TRhs, StringComparison, bool> handler)
        {
            var operatorTypes = new FilterOperatorTypes(typeof(TLhs), typeof(TRhs));
            if (handlers.ContainsKey(operatorTypes))
                handlers[operatorTypes] = handler;
            else
                handlers.Add(operatorTypes, handler);
            return this;
        }
    }
}
