using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Recipe.NetCore.Helper
{
    internal interface ISelector<T>
    {
        IOrderedQueryable<T> ApplyInitially(IQueryable<T> unsortedQuery);
        IOrderedQueryable<T> ApplySubsequently(IOrderedQueryable<T> currentQuery);
    }

    internal class Selector<TResource, TProperty> : ISelector<TResource>
    {
        private readonly bool _isDescending;
        private readonly Expression<Func<TResource, TProperty>> _propertyAccessorExpression;

        public Selector(bool isDescending, Expression<Func<TResource, TProperty>> propertyAccessorExpression)
        {
            _isDescending = isDescending;
            _propertyAccessorExpression = propertyAccessorExpression;
        }

        public IOrderedQueryable<TResource> ApplyInitially(IQueryable<TResource> unsortedQuery)
        {
            if (_isDescending)
            { return unsortedQuery.OrderByDescending(_propertyAccessorExpression); }

            return unsortedQuery.OrderBy(_propertyAccessorExpression);
        }

        public IOrderedQueryable<TResource> ApplySubsequently(IOrderedQueryable<TResource> currentQuery)
        {
            if (_isDescending)
            { return currentQuery.ThenByDescending(_propertyAccessorExpression); }

            return currentQuery.ThenBy(_propertyAccessorExpression);
        }
    }


    public static class JsonapiefQueryBuilder
    {
        private const string SortQueryParamKey = "sort";
        private const int DefaultPageSize = 100;
        private const string PageNumberQueryParam = "page.number";
        private const string PageSizeQueryParam = "page.size";


        public static IQueryable<T> GenerateQuery<T>(this IQueryable<T> query, JsonapiRequest request)
        {
            if (request != null)
            {
                query = query.GenerateFilterQuery<T>(request.Filters);
                query = query.GenerateSortQuery<T>(request.Sort);
                query = query.GeneratePagination<T>(request.Pagination);
            }
            return query;
        }

        public static IQueryable<T> GenerateSortQuery<T>(this IQueryable<T> query, List<string> sortExpressions)
        {
            if (sortExpressions == null || sortExpressions.Count == 0)
            { return query; }
            var selectors = new List<ISelector<T>>();
            var usedProperties = new Dictionary<PropertyInfo, object>();


            foreach (var sortExpression in sortExpressions)
            {
                if (string.IsNullOrEmpty(sortExpression))
                {
                    throw new KeyNotFoundException("One of the sort expressions is empty.");
                }

                bool ascending;
                string fieldName;
                if (sortExpression[0] == '-')
                {
                    ascending = false;
                    fieldName = sortExpression.Substring(1);
                }
                else
                {
                    ascending = true;
                    fieldName = sortExpression;
                }

                if (string.IsNullOrWhiteSpace(fieldName))
                {
                    throw new KeyNotFoundException("One of the sort expressions is empty.");
                }

                var paramExpr = Expression.Parameter(typeof(T));
                Expression sortValueExpression;

                var property = typeof(T).GetJsonApiProperty(fieldName);
                if (property == null)
                {
                    continue;
                }

                if (usedProperties.ContainsKey(property))
                {
                    continue;
                }

                usedProperties[property] = null;
                sortValueExpression = Expression.Property(paramExpr, property);
                var lambda = Expression.Lambda(sortValueExpression, paramExpr);
                var selectorType = typeof(Selector<,>).MakeGenericType(typeof(T), sortValueExpression.Type);
                var selector = Activator.CreateInstance(selectorType, !ascending, lambda);

                selectors.Add((ISelector<T>)selector);
            }

            var firstSelector = selectors.First();

            IOrderedQueryable<T> workingQuery = firstSelector.ApplyInitially(query);
            query = selectors.Skip(1).Aggregate(workingQuery, (current, selector) => selector.ApplySubsequently(current));
            return query;

        }

        private static PropertyInfo GetJsonApiProperty(this Type type, string propertyName)
        {
            string name = propertyName.Replace("-", string.Empty, StringComparison.CurrentCulture).ToLower(CultureInfo.CurrentCulture);
            var property = type.GetProperties().FirstOrDefault(x => x.Name.ToLower(CultureInfo.CurrentCulture) == name);
            return property;
        }

        public static IQueryable<T> GenerateFilterQuery<T>(this IQueryable<T> query, Dictionary<string, string> filters)
        {
            Expression expr = null;
            var param = Expression.Parameter(typeof(T));

            if (filters != null && filters.Keys.Count > 0)
            {

                foreach (var key in filters.Keys)
                {
                    var property = typeof(T).GetJsonApiProperty(key);
                    Expression propertyExpr = null;
                    if (property != null)
                    {
                        propertyExpr = GetPredicateBodyForProperty(property, filters[key], param);
                    }

                    if (expr != null)
                    {
                        expr = Expression.AndAlso(expr, propertyExpr);
                    }
                    else
                    {
                        expr = propertyExpr;
                    }
                }
            }
            if (expr != null)
            {
                var lambdaExpr = Expression.Lambda<Func<T, bool>>(expr, param);
                query = query.Where(lambdaExpr);
            }
            return query;
        }


        private static Expression GetPredicateBodyForProperty(PropertyInfo prop, string queryValue, ParameterExpression param)
        {
            var propertyType = prop.PropertyType;

            Expression expr;
            if (propertyType == typeof(String))
            {
                if (String.IsNullOrWhiteSpace(queryValue))
                {
                    Expression propertyExpr = Expression.Property(param, prop);
                    expr = Expression.Equal(propertyExpr, Expression.Constant(null));
                }
                else
                {
                    Expression propertyExpr = Expression.Property(param, prop);
                    expr = Expression.Equal(propertyExpr, Expression.Constant(queryValue));
                }
            }
            else if (propertyType == typeof(Boolean))
            {
                bool value;
                expr = bool.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Boolean?))
            {
                bool tmp;
                var value = bool.TryParse(queryValue, out tmp) ? tmp : (bool?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(SByte))
            {
                SByte value;
                expr = SByte.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(SByte?))
            {
                SByte tmp;
                var value = SByte.TryParse(queryValue, out tmp) ? tmp : (SByte?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(Byte))
            {
                Byte value;
                expr = Byte.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Byte?))
            {
                Byte tmp;
                var value = Byte.TryParse(queryValue, out tmp) ? tmp : (Byte?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(Int16))
            {
                Int16 value;
                expr = Int16.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Int16?))
            {
                Int16 tmp;
                var value = Int16.TryParse(queryValue, out tmp) ? tmp : (Int16?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(UInt16))
            {
                UInt16 value;
                expr = UInt16.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(UInt16?))
            {
                UInt16 tmp;
                var value = UInt16.TryParse(queryValue, out tmp) ? tmp : (UInt16?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(Int32))
            {
                Int32 value;
                expr = Int32.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Int32?))
            {
                Int32 tmp;
                var value = Int32.TryParse(queryValue, out tmp) ? tmp : (Int32?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(UInt32))
            {
                UInt32 value;
                expr = UInt32.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(UInt32?))
            {
                UInt32 tmp;
                var value = UInt32.TryParse(queryValue, out tmp) ? tmp : (UInt32?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(Int64))
            {
                Int64 value;
                expr = Int64.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Int64?))
            {
                Int64 tmp;
                var value = Int64.TryParse(queryValue, out tmp) ? tmp : (Int64?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(UInt64))
            {
                UInt64 value;
                expr = UInt64.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(UInt64?))
            {
                UInt64 tmp;
                var value = UInt64.TryParse(queryValue, out tmp) ? tmp : (UInt64?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(Single))
            {
                Single value;
                expr = Single.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Single?))
            {
                Single tmp;
                var value = Single.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out tmp) ? tmp : (Single?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(Double))
            {
                Double value;
                expr = Double.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Double?))
            {
                Double tmp;
                var value = Double.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out tmp) ? tmp : (Double?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(Decimal))
            {
                Decimal value;
                expr = Decimal.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(Decimal?))
            {
                Decimal tmp;
                var value = Decimal.TryParse(queryValue, NumberStyles.Any, CultureInfo.InvariantCulture, out tmp) ? tmp : (Decimal?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(DateTime))
            {
                DateTime value;
                expr = DateTime.TryParse(queryValue, out value)
                    ? GetPropertyExpression(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(DateTime?))
            {
                DateTime tmp;
                var value = DateTime.TryParse(queryValue, out tmp) ? tmp : (DateTime?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else if (propertyType == typeof(DateTimeOffset))
            {
                DateTimeOffset value;
                expr = DateTimeOffset.TryParse(queryValue, out value)
                    ? GetPropertyExpression<DateTimeOffset>(value, prop, param)
                    : Expression.Constant(false);
            }
            else if (propertyType == typeof(DateTimeOffset?))
            {
                DateTimeOffset tmp;
                var value = DateTimeOffset.TryParse(queryValue, out tmp) ? tmp : (DateTimeOffset?)null;
                expr = GetPropertyExpression(value, prop, param);
            }
            else
            {
                expr = Expression.Constant(true);
            }

            return expr;
        }

        private static Expression GetPropertyExpression<T>(T value, PropertyInfo property,
         ParameterExpression param)
        {
            Expression propertyExpr = Expression.Property(param, property);
            var valueExpr = Expression.Constant(value);
            Expression castedConstantExpr = Expression.Convert(valueExpr, typeof(T));
            return Expression.Equal(propertyExpr, castedConstantExpr);
        }

        public static IQueryable<T> GeneratePagination<T>(this IQueryable<T> query, JsonapiPagination pagination)
        {
            if (pagination != null)
            {
                if (pagination.PageSize > 100)
                {
                    throw new InvalidCastException("Page size must be less than 100");
                }

                query = query.Skip((pagination.PageNumber - 1) * pagination.PageSize).Take(pagination.PageSize);
            }
            return query;
        }

        public static JsonapiRequest GetJsonApiRequest(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            if (parameters.Any())
            {
                JsonapiRequest jsonAPIRequest = new JsonapiRequest();
                jsonAPIRequest.Sort = ExtractSortExpressions(parameters);
                jsonAPIRequest.Filters = ExtractFilters(parameters);
                jsonAPIRequest.Pagination = ExtractPagination(parameters);

                return jsonAPIRequest;
            }
            return null;
        }

        private static JsonapiPagination ExtractPagination(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            var hasPageNumberParam = parameters.Any(e => e.Key == PageNumberQueryParam);
            var hasPageSizeParam = parameters.Any(e => e.Key == PageSizeQueryParam);
            var pageNumber = 0;
            var pageSize = DefaultPageSize;

            if (!hasPageNumberParam && !hasPageSizeParam) { return null; }

            if (hasPageNumberParam && !int.TryParse(parameters.FirstOrDefault(e => e.Key == PageNumberQueryParam).Value, out pageNumber))
            { throw new InvalidCastException("Page number must be a positive integer."); }

            if (hasPageSizeParam && !int.TryParse(parameters.FirstOrDefault(e => e.Key == PageSizeQueryParam).Value, out pageSize))
            { throw new InvalidCastException("Page size must be a positive integer."); }

            if (pageNumber < 0)
            {
                throw new NotSupportedException("Page number must not be negative.");
            }

            if (pageSize <= 0)
            {
                throw new NotSupportedException("Page size must be greater than or equal to 1.");
            }


            JsonapiPagination pagination = new JsonapiPagination();
            pagination.PageNumber = pageNumber;
            pagination.PageSize = pageSize;

            return pagination;
        }



        private static Dictionary<string, string> ExtractFilters(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            Dictionary<string, string> filters = new Dictionary<string, string>();
            foreach (var queryPair in parameters)
            {
                if (String.IsNullOrWhiteSpace(queryPair.Key))
                { continue; }

                if (!queryPair.Key.StartsWith("filter."))
                { continue; }

                var filterField = queryPair.Key.Substring(7); // Skip "filter."
                filters.Add(filterField, queryPair.Value);
            }

            return filters;

        }



        private static List<string> ExtractSortExpressions(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            var sortParam = parameters.FirstOrDefault(kvp => kvp.Key == SortQueryParamKey);

            if (sortParam.Key != SortQueryParamKey)
            { return new List<string>(); }

            return sortParam.Value.Split(',').ToList();
        }

    }

    public class JsonapiRequest
    {
        public List<string> Sort { get; set; }

        public Dictionary<string, string> Filters { get; set; }

        public JsonapiPagination Pagination { get; set; }
    }

    public class JsonapiPagination
    {
        public int PageSize { get; set; }

        public int PageNumber { get; set; }
    }
}
