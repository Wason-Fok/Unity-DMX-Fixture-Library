using System;
using System.Collections.Generic;
using System.Globalization;

namespace Unity.QuickSearch
{
    internal interface IFilterOperation
    {
        string filterName { get; }
        string filterValue { get; }
        string filterParams { get; }
        IFilter filter { get; }
        FilterOperator filterOperator { get; }
        string ToString();
    }

    internal interface IFilterOperationGenerator<TData>
    {
        IFilterOperation GenerateOperation(int operatorIndex, List<QueryError> errors, QueryEngineImpl<TData> engine);
    }

    internal class FilterOperationGenerator<TObject, TFilterVariable, TFilterConstant> : IFilterOperationGenerator<TObject>
    {
        protected readonly Filter<TObject, TFilterVariable> m_Filter;
        protected readonly FilterOperator m_Operator;
        protected readonly string m_FilterValue;
        protected readonly ParseResult<TFilterConstant> m_FilterValueParseResult;

        public FilterOperationGenerator(Filter<TObject, TFilterVariable> filter, FilterOperator op, string filterValue, IParseResult filterValueParseResult)
        {
            m_Filter = filter;
            m_Operator = op;
            m_FilterValue = filterValue;
            m_FilterValueParseResult = (ParseResult<TFilterConstant>)filterValueParseResult;
        }

        public virtual IFilterOperation GenerateOperation(int operatorIndex, List<QueryError> errors, QueryEngineImpl<TObject> engine)
        {
            Func<TObject, bool> operation = o => false;
            var filterValue = m_FilterValueParseResult.parsedValue;
            var handlerTypedKey = new FilterOperatorTypes(typeof(TFilterVariable), typeof(TFilterConstant));
            var handlerGenericKey = new FilterOperatorTypes(typeof(object), typeof(object));
            var stringComparisonOptions = m_Filter.overrideStringComparison ? m_Filter.stringComparison : engine.globalStringComparison;

            var error = "";

            if (m_Operator.handlers.ContainsKey(handlerTypedKey))
            {
                if (!(m_Operator.handlers[handlerTypedKey] is Func<TFilterVariable, TFilterConstant, StringComparison, bool> handler))
                {
                    error = $"Filter operator handler of type ({handlerTypedKey.leftHandSideType}, {handlerTypedKey.rightHandSideType}) could not be" +
                        $" converted to Func<{handlerTypedKey.leftHandSideType}, {handlerTypedKey.rightHandSideType}, bool>";
                    errors.Add(new QueryError(operatorIndex, error));
                }
                else
                    operation = o => handler(m_Filter.GetData(o), filterValue, stringComparisonOptions);
            }
            else if (m_Operator.handlers.ContainsKey(handlerGenericKey))
            {
                if (!(m_Operator.handlers[handlerGenericKey] is Func<object, object, StringComparison, bool> handler))
                {
                    error = $"Filter operator handler of type ({handlerGenericKey.leftHandSideType}, {handlerGenericKey.rightHandSideType}) could not be" +
                        $" converted to Func<{handlerGenericKey.leftHandSideType}, {handlerGenericKey.rightHandSideType}, bool>";
                    errors.Add(new QueryError(operatorIndex, error));
                }
                else
                    operation = o => handler(m_Filter.GetData(o), filterValue, stringComparisonOptions);
            }
            else
            {
                error = $"No handler of type ({typeof(TFilterVariable)}, {typeof(TFilterConstant)}) or (object, object) found for operator {m_Operator.token}";
            }


            if (!string.IsNullOrEmpty(error))
            {
                errors.Add(new QueryError(operatorIndex, m_Operator.token.Length, error));
            }

            return new FilterOperation<TObject, TFilterVariable>(m_Filter, m_Operator, m_FilterValue, operation);
        }
    }

    internal class FilterResolverOperationGenerator<TObject, TFilterVariable> : FilterOperationGenerator<TObject, TFilterVariable, TFilterVariable>
    {
        public FilterResolverOperationGenerator(Filter<TObject, TFilterVariable> filter, FilterOperator op, string filterValue, IParseResult filterValueParseResult)
            : base(filter, op, filterValue, filterValueParseResult)
        { }

        public override IFilterOperation GenerateOperation(int operatorIndex, List<QueryError> errors, QueryEngineImpl<TObject> engine)
        {
            var filterValue = m_FilterValueParseResult.parsedValue;
            // ReSharper disable once ConvertToLocalFunction
            Func<TObject, bool> operation = o => m_Filter.Resolve(o, m_Operator, filterValue);

            return new FilterOperation<TObject, TFilterVariable>(m_Filter, m_Operator, m_FilterValue, operation);
        }
    }

    internal class FilterOperationGenerator<TObject, TParam, TFilterVariable, TFilterConstant> : IFilterOperationGenerator<TObject>
    {
        protected readonly Filter<TObject, TParam, TFilterVariable> m_Filter;
        protected readonly FilterOperator m_Operator;
        protected readonly string m_FilterValue;
        protected readonly string m_ParamValue;
        protected readonly ParseResult<TFilterConstant> m_FilterValueParseResult;

        public FilterOperationGenerator(Filter<TObject, TParam, TFilterVariable> filter, FilterOperator op, string filterValue, string paramValue, IParseResult filterValueParseResult)
        {
            m_Filter = filter;
            m_Operator = op;
            m_FilterValue = filterValue;
            m_ParamValue = paramValue;
            m_FilterValueParseResult = (ParseResult<TFilterConstant>)filterValueParseResult;
        }

        public virtual IFilterOperation GenerateOperation(int operatorIndex, List<QueryError> errors, QueryEngineImpl<TObject> engine)
        {
            Func<TObject, TParam, bool> operation = (o, p) => false;
            var filterValue = m_FilterValueParseResult.parsedValue;
            var handlerTypedKey = new FilterOperatorTypes(typeof(TFilterVariable), typeof(TFilterConstant));
            var handlerGenericKey = new FilterOperatorTypes(typeof(object), typeof(object));
            var stringComparisonOptions = m_Filter.overrideStringComparison ? m_Filter.stringComparison : engine.globalStringComparison;

            var error = "";

            if (m_Operator.handlers.ContainsKey(handlerTypedKey))
            {
                if (!(m_Operator.handlers[handlerTypedKey] is Func<TFilterVariable, TFilterConstant, StringComparison, bool> handler))
                {
                    error = $"Filter operator handler of type ({handlerTypedKey.leftHandSideType}, {handlerTypedKey.rightHandSideType}) could not be" +
                        $" converted to Func<{handlerTypedKey.leftHandSideType}, {handlerTypedKey.rightHandSideType}, bool>";
                    errors.Add(new QueryError(operatorIndex, error));
                }
                else
                    operation = (o, p) => handler(m_Filter.GetData(o, p), filterValue, stringComparisonOptions);
            }
            else if (m_Operator.handlers.ContainsKey(handlerGenericKey))
            {
                if (!(m_Operator.handlers[handlerGenericKey] is Func<object, object, StringComparison, bool> handler))
                {
                    error = $"Filter operator handler of type ({handlerGenericKey.leftHandSideType}, {handlerGenericKey.rightHandSideType}) could not be" +
                        $" converted to Func<{handlerGenericKey.leftHandSideType}, {handlerGenericKey.rightHandSideType}, bool>";
                    errors.Add(new QueryError(operatorIndex, error));
                }
                else
                    operation = (o, p) => handler(m_Filter.GetData(o, p), filterValue, stringComparisonOptions);
            }
            else
            {
                error = $"No handler of type ({typeof(TFilterVariable)}, {typeof(TFilterConstant)}) or (object, object) found for operator {m_Operator.token}";
            }


            if (!string.IsNullOrEmpty(error))
            {
                errors.Add(new QueryError(operatorIndex, m_Operator.token.Length, error));
            }

            return new FilterOperation<TObject, TParam, TFilterVariable>(m_Filter, m_Operator, m_FilterValue, m_ParamValue, operation);
        }
    }

    internal class FilterResolverOperationGenerator<TObject, TParam, TFilterVariable> : FilterOperationGenerator<TObject, TParam, TFilterVariable, TFilterVariable>
    {
        public FilterResolverOperationGenerator(Filter<TObject, TParam, TFilterVariable> filter, FilterOperator op, string filterValue, string paramValue, IParseResult filterValueParseResult)
            : base(filter, op, filterValue, paramValue, filterValueParseResult)
        { }

        public override IFilterOperation GenerateOperation(int operatorIndex, List<QueryError> errors, QueryEngineImpl<TObject> engine)
        {
            var filterValue = m_FilterValueParseResult.parsedValue;
            // ReSharper disable once ConvertToLocalFunction
            Func<TObject, TParam, bool> operation = (o, param) => m_Filter.Resolve(o, param, m_Operator, filterValue);

            return new FilterOperation<TObject, TParam, TFilterVariable>(m_Filter, m_Operator, m_FilterValue, m_ParamValue, operation);
        }
    }

    internal abstract class BaseFilterOperation<TObject> : IFilterOperation
    {
        public string filterName => filter.token;
        public string filterValue { get; }
        public virtual string filterParams => null;
        public IFilter filter { get; }
        public FilterOperator filterOperator { get; }

        protected BaseFilterOperation(IFilter filter, FilterOperator filterOperator, string filterValue)
        {
            this.filter = filter;
            this.filterOperator = filterOperator;
            this.filterValue = filterValue;
        }

        public abstract bool Match(TObject obj);

        public new virtual string ToString()
        {
            return $"{filterName}{filterOperator.token}{filterValue}";
        }
    }

    internal class FilterOperation<TObject, TFilterVariable> : BaseFilterOperation<TObject>
    {
        public Func<TObject, bool> operation { get; }

        public FilterOperation(Filter<TObject, TFilterVariable> filter, FilterOperator filterOperator, string filterValue, Func<TObject, bool> operation)
            : base(filter, filterOperator, filterValue)
        {
            this.operation = operation;
        }

        public override bool Match(TObject obj)
        {
            return operation(obj);
        }
    }

    internal class FilterOperation<TObject, TParam, TFilter> : BaseFilterOperation<TObject>
    {
        private string m_ParamValue;

        public Func<TObject, TParam, bool> operation { get; }
        public TParam param { get; }

        public override string filterParams => m_ParamValue;

        public FilterOperation(Filter<TObject, TParam, TFilter> filter, FilterOperator filterOperator, string filterValue, Func<TObject, TParam, bool> operation)
            : base(filter, filterOperator, filterValue)
        {
            this.operation = operation;
            m_ParamValue = null;
            param = default;
        }

        public FilterOperation(Filter<TObject, TParam, TFilter> filter, FilterOperator filterOperator, string filterValue, string paramValue, Func<TObject, TParam, bool> operation)
            : base(filter, filterOperator, filterValue)
        {
            this.operation = operation;
            m_ParamValue = paramValue;
            param = string.IsNullOrEmpty(paramValue) ? default : filter.TransformParameter(paramValue);
        }

        public override bool Match(TObject obj)
        {
            return operation(obj, param);
        }

        public override string ToString()
        {
            var paramString = string.IsNullOrEmpty(m_ParamValue) ? "" : $"({m_ParamValue})";
            return $"{filterName}{paramString}{filterOperator.token}{filterValue}";
        }
    }
}
