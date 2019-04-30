using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using LinqKit;
using Microsoft.AspNetCore.Http;
using Recipe.NetCore.Base.Abstract;
using Recipe.NetCore.Helper;

namespace Recipe.NetCore.Helper
{
    public class QueryHelper
    {
        public static bool PropertyExists<T>(string propertyName)
        {
            if (!string.IsNullOrEmpty(propertyName))
            {
                return typeof(T).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) != null;
            }
            return false;
        }

        private delegate void ProcessQueryString<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null);

        private static Dictionary<Regex, ProcessQueryString<T>> GetQueryParamAndMethods<T>()
        {
            return new Dictionary<Regex, ProcessQueryString<T>>()
                {
                    { new Regex(@"^page\[number\]$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupPageNumber)},
                    { new Regex(@"^page\[size\]$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupPageSize)},
                    { new Regex(@"^filters\[-\S*\]$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupNotInFilters)},
                    { new Regex(@"^filters\[=\S*\]$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupEqualFilters)},
                    { new Regex(@"^filters\[<=\S*\]$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupLessThanEqualToFilters)},
                     { new Regex(@"^filters\[>=\S*\]$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupGreaterThanEqualToFilters)},
                    { new Regex(@"^filters\[\S*\]$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupFilters)},
                    { new Regex(@"^sort$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupOrderBy)},
                    { new Regex(@"^includes$", RegexOptions.IgnoreCase), new ProcessQueryString<T>(QueryStringParser.SetupIncludes)}
                };
        }

        public static DataTransferObject<T> Get<T>(IQueryCollection QueryString, List<string> ExludeProperties)
        {
            var model = new DataTransferObject<T>();
            model.Paging = new Paging()
            {
                PageNumber = 1, // Default Values
                PageSize = 10
            };
            model.Filter = f => true;

            var QueryParamAndMethods = GetQueryParamAndMethods<T>();

            model.Result = Activator.CreateInstance<T>();
            foreach (var key in QueryString.Keys)
            {
                var method = QueryParamAndMethods.Where(x => key != null && x.Key.IsMatch(key));
                if (method.Any())
                {
                    method.First().Value.Invoke(model, key, QueryString[key], ExludeProperties);
                }
            }

            return model;
        }

        public static DataTransferObject<T> Get<T>(IQueryCollection QueryString)
        {
            return Get<T>(QueryString, new List<string>());
        }

        public static T GetModel<T>(string QueryString) where T : new()
        {
            Type type = typeof(T);
            var destObject = Activator.CreateInstance<T>();
            if (!string.IsNullOrWhiteSpace(QueryString))
            {
                var nameValuePaire = System.Web.HttpUtility.ParseQueryString(QueryString.Substring(1));
                foreach (string key in nameValuePaire.AllKeys)
                {
                    PropertyInfo p = type.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (p != null)
                    {
                        if (p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                        {
                            p.SetValue(destObject, Convert.ChangeType(nameValuePaire[key], Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType), null);
                        }
                        else
                            if (StringHelper.CanConvert(p.PropertyType, nameValuePaire[key]))
                            p.SetValue(destObject, Convert.ChangeType(nameValuePaire[key], p.PropertyType), null);
                    }
                }
            }
            return destObject;
        }

        public static string[] GetIncludeDetails(string QueryString)
        {
            if (!string.IsNullOrWhiteSpace(QueryString))
            {
                var nameValuePaire = System.Web.HttpUtility.ParseQueryString(QueryString.Substring(1));

                if (nameValuePaire["includes"] != null)
                {
                    string[] includeValues = nameValuePaire["includes"].Split(new char[1] { ',' });
                    return includeValues;
                }
            }
            return new string[0];
        }
    }

    public static class SortType
    {
        public static readonly string Ascending = "+";
        public static readonly string Descending = "-";
    }

    public class QueryStringParser
    {
        protected QueryStringParser()
        {

        }

        public static void SetupPageNumber<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            var Number = 0;
            if (int.TryParse(Value, out Number))
            {
                Model.Paging.PageNumber = Number;
            }
            else
            {
                throw new ArgumentException("Invalid Page Number");
            }
        }

        public static void SetupPageSize<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            var Size = 0;
            if (int.TryParse(Value, out Size))
            {
                Model.Paging.PageSize = Size;
            }
            else
            {
                throw new ArgumentException("Invalid Page Size");
            }
        }

        public static void SetupIncludes<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            if (!string.IsNullOrEmpty(Value))
            {
                Type type = Model.Result.GetType();
                List<Expression<Func<T, object>>> Includes = new List<Expression<Func<T, object>>>();
                string[] includeValues = Value.Split(new char[1] { ',' });
                if (includeValues != null && includeValues.Any())
                {
                    foreach (string include in includeValues)
                    {
                        PropertyInfo property = type.GetProperty(include.Replace("-", ""), BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                        if (property != null && !ExludeProperties.Contains(property.Name))
                        {
                            Includes.Add(Build<T>(property.Name));
                        }
                    }
                }

                if (Includes.Any())
                    Model.Includes = Includes;
            }
        }

        public static Expression<Func<TClass, object>> Build<TClass>(string fieldName)
        {
            var param = Expression.Parameter(typeof(TClass));
            var field = Expression.PropertyOrField(param, fieldName);
            return Expression.Lambda<Func<TClass, object>>(field, param);
        }

        public static void SetupNotInFilters<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            // need to add support for more columns
            // for now supporting only NotIn on Id.
            Type type = Model.Result.GetType();
            PropertyInfo property = type.GetProperty("Id", BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
            if (Value != null)
            {
                try
                {
                    Expression propertyExpress = Expression.Property(parameter, property.Name);
                    var target = Expression.Constant(Value.Split(',').Select(Int64.Parse).ToList());
                    var notContainsMethodExp = Expression.Not(Expression.Call(typeof(Enumerable), "Contains", new[] { propertyExpress.Type }, target, propertyExpress));
                    Model.Filter = Model.Filter.And(Expression.Lambda<Func<T, bool>>(notContainsMethodExp, parameter));
                }
                catch
                {
                    //do nothing for now
                }
            }
        }

        public static void SetupEqualFilters<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            // for now supporting only Equal on one column only.
            Key = Key.Replace("filters[", "").Replace("]", "").Replace("=", "");

            Type type = Model.Result.GetType();
            MethodInfo containsMethod = typeof(string).GetMethod("Equals", new[] { typeof(string) });
            MethodInfo toLowerMethod = typeof(string).GetMethod("ToLower", System.Type.EmptyTypes);
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

            Expression<Func<T, bool>> resultExpression = null;

            string[] Keys = null;
            Keys = new string[1] { Key };

            foreach (string key in Keys)
            {
                if (key != null && key.Contains("."))
                {
                    string[] properties = key.Split(new char[1] { '.' });

                    PropertyInfo propertyInfo = null;
                    Expression entityExpression = null;
                    Expression<Func<T, bool>> expressionForNull = x => true;

                    string excludedProperty = "";
                    foreach (string prop in properties)
                    {
                        if (excludedProperty == "")
                            excludedProperty = string.Concat(excludedProperty, prop);
                        else
                            excludedProperty = string.Concat(excludedProperty, "." + prop);

                        if (ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase))
                        {
                            propertyInfo = null;
                            break;
                        }

                        if (propertyInfo != null)
                        {
                            Type propertyType = propertyInfo.PropertyType;
                            propertyInfo = propertyType.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(entityExpression, prop);
                        }
                        else
                        {
                            propertyInfo = type.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(parameter, prop);
                        }

                        if (propertyInfo != null && !ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase) &&
                            propertyInfo.PropertyType.IsClass && !propertyInfo.PropertyType.FullName.StartsWith("System."))
                        {
                            var binaryExpressionForNullObject = Expression.NotEqual(entityExpression, Expression.Constant(null, propertyInfo.PropertyType));
                            var expressionForNullObject = Expression.Lambda<Func<T, bool>>(binaryExpressionForNullObject, parameter);

                            if (expressionForNull == null)
                                expressionForNull = expressionForNullObject;
                            else
                                expressionForNull = expressionForNull.And(expressionForNullObject);

                        }
                    }

                    if (propertyInfo == null)
                        return;

                    Expression target = Expression.Constant(Convert.ChangeType(Value.Trim().ToLower(), propertyInfo.PropertyType), propertyInfo.PropertyType);
                    if (propertyInfo.PropertyType == typeof(string))
                    {
                        var binaryExpressionForNull = Expression.NotEqual(entityExpression, Expression.Constant(null, typeof(string)));
                        var toLowerMethodExpression = Expression.Call(entityExpression, toLowerMethod);
                        var containsMethodExp = Expression.Call(toLowerMethodExpression, containsMethod, target);
                        var expressionForNullProperty = Expression.Lambda<Func<T, bool>>(binaryExpressionForNull, parameter);
                        var expressionForContains = Expression.Lambda<Func<T, bool>>(containsMethodExp, parameter);
                        if (resultExpression == null)
                        {
                            resultExpression = expressionForNull.And(expressionForNullProperty.And(expressionForContains));
                        }
                        else
                        {
                            resultExpression = resultExpression.Or(expressionForNull.And(expressionForNullProperty.And(expressionForContains)));
                        }
                    }
                    else if ((propertyInfo.PropertyType == typeof(Int64) && Convert.ToInt64(Value) > 0) ||
                        (propertyInfo.PropertyType == typeof(bool) && !Convert.ToBoolean(Value)))
                    {
                        Expression equalsMethod = Expression.Equal(entityExpression, target);
                        if (resultExpression == null)
                        {
                            resultExpression = expressionForNull.And(Expression.Lambda<Func<T, bool>>(equalsMethod, parameter));
                        }
                        else
                        {
                            resultExpression = resultExpression.Or(expressionForNull.And(Expression.Lambda<Func<T, bool>>(equalsMethod, parameter)));
                        }
                    }
                }
                else
                {
                    PropertyInfo filterProperty = type.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (filterProperty != null && Value != null && !ExludeProperties.Contains(filterProperty.Name, StringComparer.OrdinalIgnoreCase) && StringHelper.CanConvert(filterProperty.PropertyType, Value))
                    {
                        var convertedValue = Convert.ChangeType(Value, filterProperty.PropertyType);
                        filterProperty.SetValue(Model.Result, convertedValue, null);
                        var PropertyType = filterProperty.PropertyType;

                        Expression propertyExpress = Expression.Property(parameter, filterProperty.Name);
                        Expression target = Expression.Constant(Convert.ChangeType(Value.Trim().ToLower(), PropertyType), PropertyType);

                        if (PropertyType == typeof(string))
                        {
                            var toLowerMethodExpression = Expression.Call(propertyExpress, toLowerMethod);
                            var containsMethodExp = Expression.Call(toLowerMethodExpression, containsMethod, target);

                            var binaryExpressionForNull = Expression.NotEqual(propertyExpress, Expression.Constant(null, typeof(string)));
                            var expressionForNullProperty = Expression.Lambda<Func<T, bool>>(binaryExpressionForNull, parameter);

                            if (resultExpression == null)
                            {
                                resultExpression = expressionForNullProperty.And(Expression.Lambda<Func<T, bool>>(containsMethodExp, parameter));
                            }
                            else
                            {
                                resultExpression = resultExpression.Or(expressionForNullProperty.And(Expression.Lambda<Func<T, bool>>(containsMethodExp, parameter)));
                            }
                        }
                        else if ((PropertyType == typeof(Int64) && Convert.ToInt64(Value) > 0) ||
                            (PropertyType == typeof(bool)))
                        {
                            Expression equalsMethod = Expression.Equal(propertyExpress, target);
                            if (resultExpression == null)
                            {
                                resultExpression = Expression.Lambda<Func<T, bool>>(equalsMethod, parameter);
                            }
                            else
                            {
                                resultExpression = resultExpression.Or(Expression.Lambda<Func<T, bool>>(equalsMethod, parameter));
                            }
                        }
                    }
                }
            }

            if (resultExpression != null)
                Model.Filter = Model.Filter.And(resultExpression);
        }

        public static void SetupFilters<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            Key = Key.Replace("filters[", "").Replace("]", "");

            Type type = Model.Result.GetType();
            MethodInfo containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            MethodInfo toLowerMethod = typeof(string).GetMethod("ToLower", System.Type.EmptyTypes);
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

            Expression<Func<T, bool>> resultExpression = null;

            string[] Keys = null;
            if (Key.Contains("+"))
            {
                Keys = Key.Split(new char[1] { '+' });
            }
            else
            {
                Keys = new string[1] { Key };
            }
            foreach (string key in Keys)
            {
                if (key != null && key.Contains("."))
                {
                    string[] properties = key.Split(new char[1] { '.' });

                    PropertyInfo propertyInfo = null;
                    Expression entityExpression = null;
                    Expression<Func<T, bool>> expressionForNull = x => true;

                    string excludedProperty = "";
                    foreach (string prop in properties)
                    {
                        if (excludedProperty == "")
                            excludedProperty = string.Concat(excludedProperty, prop);
                        else
                            excludedProperty = string.Concat(excludedProperty, "." + prop);

                        if (ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase))
                        {
                            propertyInfo = null;
                            break;
                        }

                        if (propertyInfo != null)
                        {
                            Type propertyType = propertyInfo.PropertyType;
                            propertyInfo = propertyType.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(entityExpression, prop);
                        }
                        else
                        {
                            propertyInfo = type.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(parameter, prop);
                        }

                        if (propertyInfo != null && !ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase) &&
                            propertyInfo.PropertyType.IsClass && !propertyInfo.PropertyType.FullName.StartsWith("System."))
                        {
                            var binaryExpressionForNullObject = Expression.NotEqual(entityExpression, Expression.Constant(null, propertyInfo.PropertyType));
                            var expressionForNullObject = Expression.Lambda<Func<T, bool>>(binaryExpressionForNullObject, parameter);

                            if (expressionForNull == null)
                                expressionForNull = expressionForNullObject;
                            else
                                expressionForNull = expressionForNull.And(expressionForNullObject);

                        }
                    }

                    if (propertyInfo == null)
                        return;

                    Expression target = Expression.Constant(Convert.ChangeType(Value.Trim().ToLower(), propertyInfo.PropertyType), propertyInfo.PropertyType);
                    if (propertyInfo.PropertyType == typeof(string))
                    {
                        var binaryExpressionForNull = Expression.NotEqual(entityExpression, Expression.Constant(null, typeof(string)));
                        var toLowerMethodExpression = Expression.Call(entityExpression, toLowerMethod);
                        var containsMethodExp = Expression.Call(toLowerMethodExpression, containsMethod, target);
                        var expressionForNullProperty = Expression.Lambda<Func<T, bool>>(binaryExpressionForNull, parameter);
                        var expressionForContains = Expression.Lambda<Func<T, bool>>(containsMethodExp, parameter);
                        if (resultExpression == null)
                        {
                            resultExpression = expressionForNull.And(expressionForNullProperty.And(expressionForContains));
                        }
                        else
                        {
                            resultExpression = resultExpression.Or(expressionForNull.And(expressionForNullProperty.And(expressionForContains)));
                        }
                    }
                    else if ((propertyInfo.PropertyType == typeof(Int64) && Convert.ToInt64(Value) > 0) ||
                        (propertyInfo.PropertyType == typeof(bool) && !Convert.ToBoolean(Value)))
                    {
                        Expression equalsMethod = Expression.Equal(entityExpression, target);
                        if (resultExpression == null)
                        {
                            resultExpression = expressionForNull.And(Expression.Lambda<Func<T, bool>>(equalsMethod, parameter));
                        }
                        else
                        {
                            resultExpression = resultExpression.Or(expressionForNull.And(Expression.Lambda<Func<T, bool>>(equalsMethod, parameter)));
                        }
                    }
                    else if (propertyInfo.PropertyType == typeof(List<T>))
                    {
                        if (resultExpression == null)
                        {
                            resultExpression = expressionForNull.And(new List<T>().Filter(propertyInfo.Name, Value));
                        }
                        else
                        {
                            resultExpression = resultExpression.Or(expressionForNull.And(new List<T>().Filter(propertyInfo.Name, Value)));
                        }
                    }
                }
                else
                {
                    PropertyInfo filterProperty = type.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (filterProperty != null && Value != null && !ExludeProperties.Contains(filterProperty.Name, StringComparer.OrdinalIgnoreCase) && StringHelper.CanConvert(filterProperty.PropertyType, Value))
                    {
                        var convertedValue = Convert.ChangeType(Value, filterProperty.PropertyType);
                        filterProperty.SetValue(Model.Result, convertedValue, null);
                        var PropertyType = filterProperty.PropertyType;

                        Expression propertyExpress = Expression.Property(parameter, filterProperty.Name);
                        Expression target = Expression.Constant(Convert.ChangeType(Value.Trim().ToLower(), PropertyType), PropertyType);

                        if (PropertyType == typeof(string))
                        {
                            var toLowerMethodExpression = Expression.Call(propertyExpress, toLowerMethod);
                            var containsMethodExp = Expression.Call(toLowerMethodExpression, containsMethod, target);

                            var binaryExpressionForNull = Expression.NotEqual(propertyExpress, Expression.Constant(null, typeof(string)));
                            var expressionForNullProperty = Expression.Lambda<Func<T, bool>>(binaryExpressionForNull, parameter);

                            if (resultExpression == null)
                            {
                                resultExpression = expressionForNullProperty.And(Expression.Lambda<Func<T, bool>>(containsMethodExp, parameter));
                            }
                            else
                            {
                                resultExpression = resultExpression.Or(expressionForNullProperty.And(Expression.Lambda<Func<T, bool>>(containsMethodExp, parameter)));
                            }
                        }
                        else if (((PropertyType == typeof(Int32) || PropertyType == typeof(Int64)) && Convert.ToInt64(Value) > 0) ||
                            (PropertyType == typeof(bool)))
                        {
                            Expression equalsMethod = Expression.Equal(propertyExpress, target);
                            if (resultExpression == null)
                            {
                                resultExpression = Expression.Lambda<Func<T, bool>>(equalsMethod, parameter);
                            }
                            else
                            {
                                resultExpression = resultExpression.Or(Expression.Lambda<Func<T, bool>>(equalsMethod, parameter));
                            }
                        }
                    }
                }
            }

            if (resultExpression != null)
                Model.Filter = Model.Filter.And(resultExpression);

            if (Key.ToLower() == "keywordsearch") //Keyword search will work for just 1 level of child entity.. it will not search child within a child
            {
                Expression<Func<T, bool>> expression = null;
                Expression<Func<T, bool>> childExpression = null;
                Model.Includes = GetChildRelation(Model); //We might not need this assignment, Includes are added automatically

                foreach (var propertyInfo in Model.Result.GetType().GetProperties())
                {
                    if (propertyInfo.PropertyType == typeof(string) && !ExludeProperties.Contains(propertyInfo.Name))
                    {
                        Expression propertyExpress = Expression.Property(parameter, propertyInfo.Name);
                        Expression target = Expression.Constant(Convert.ChangeType(Value.Trim().ToLower(), propertyInfo.PropertyType), propertyInfo.PropertyType);
                        var toLowerMethodExpression = Expression.Call(propertyExpress, toLowerMethod);
                        var containsMethodExpression = Expression.Call(toLowerMethodExpression, containsMethod, target);

                        var binaryExpressionForNull = Expression.NotEqual(propertyExpress, Expression.Constant(null, typeof(string)));
                        var expressionForNull = Expression.Lambda<Func<T, bool>>(binaryExpressionForNull, parameter);
                        var expressionForContains = Expression.Lambda<Func<T, bool>>(containsMethodExpression, parameter);

                        if (expression != null)
                            expression = expression.Or(expressionForNull.And(expressionForContains));
                        else
                            expression = expressionForNull.And(expressionForContains);
                    }
                    else if (propertyInfo.PropertyType.IsClass &&
                            !propertyInfo.PropertyType.FullName.StartsWith("System.") &&
                            !ExludeProperties.Contains(propertyInfo.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        Type childType = propertyInfo.PropertyType;
                        var childInstance = Activator.CreateInstance(childType);
                        foreach (var childPropertyInfo in childInstance.GetType().GetProperties())
                        {
                            if (childPropertyInfo.PropertyType == typeof(string) && !ExludeProperties.Contains(string.Concat(childType.Name, ".", childPropertyInfo.Name), StringComparer.OrdinalIgnoreCase))
                            {
                                Expression entityExpression = Expression.Property(parameter, propertyInfo.Name);
                                var binaryExpressionForNullObject = Expression.NotEqual(entityExpression, Expression.Constant(null, childType));

                                Expression propertyExpression = Expression.Property(entityExpression, childPropertyInfo.Name);
                                Expression target = Expression.Constant(Convert.ChangeType(Value.Trim().ToLower(), childPropertyInfo.PropertyType), childPropertyInfo.PropertyType);
                                var toLowerMethodExpression = Expression.Call(propertyExpression, toLowerMethod);
                                var containsMethodExpression = Expression.Call(toLowerMethodExpression, containsMethod, target);

                                var binaryExpressionForNull = Expression.NotEqual(propertyExpression, Expression.Constant(null, typeof(string)));
                                var expressionForNullObject = Expression.Lambda<Func<T, bool>>(binaryExpressionForNullObject, parameter);
                                var expressionForNullProperty = Expression.Lambda<Func<T, bool>>(binaryExpressionForNull, parameter);
                                var expressionForContains = Expression.Lambda<Func<T, bool>>(containsMethodExpression, parameter);

                                if (childExpression != null)
                                    childExpression = childExpression.Or(expressionForNullObject.And(expressionForNullProperty.And(expressionForContains)));
                                else
                                    childExpression = expressionForNullObject.And(expressionForNullProperty.And(expressionForContains));
                            }
                        }
                        if (expression != null && childExpression != null)
                            expression = expression.Or(childExpression);
                        else if (childExpression != null)
                            expression = childExpression;
                    }
                }

                if (expression != null)
                    Model.Filter = Model.Filter.And(expression);
            }
        }

        public static void SetupOrderBy<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            string[] values = Value.Split(',');
            Type type = Model.Result.GetType();
            var defaultOrder = SortType.Ascending;
            if (values.Length > 0)
            {
                var key = values[0];
                if (key.StartsWith("-"))
                {
                    key = key.Replace("-", "");
                    defaultOrder = SortType.Descending;
                }
                else if (key.StartsWith("+"))
                {
                    key = key.Replace("+", "");
                }
                PropertyInfo property = type.GetProperty(key.Trim(), BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    var param = Expression.Parameter(typeof(T), "x");


                    if (property.PropertyType == typeof(string))
                    {
                        var Lambda = Expression.Lambda<Func<T, string>>(Expression.Convert(Expression.Property(param, property.Name), typeof(string)), param);
                        if (defaultOrder == SortType.Ascending)
                        {
                            Model.OrderBy = source => source.OrderBy(Lambda);
                        }
                        else
                        {
                            Model.OrderBy = source => source.OrderByDescending(Lambda);
                        }
                    }
                    else if (property.PropertyType == typeof(Int64) ||
                        (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(property.PropertyType) == typeof(Int64)))
                    {
                        var Lambda = Expression.Lambda<Func<T, Int64>>(Expression.Convert(Expression.Property(param, property.Name), typeof(Int64)), param);
                        if (defaultOrder == SortType.Ascending)
                        {
                            Model.OrderBy = source => source.OrderBy(Lambda);
                        }
                        else
                        {
                            Model.OrderBy = source => source.OrderByDescending(Lambda);
                        }
                    }
                    else if (property.PropertyType == typeof(Int32) ||
                       (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(property.PropertyType) == typeof(Int32)))
                    {
                        var Lambda = Expression.Lambda<Func<T, Int32>>(Expression.Convert(Expression.Property(param, property.Name), typeof(Int32)), param);
                        if (defaultOrder == SortType.Ascending)
                        {
                            Model.OrderBy = source => source.OrderBy(Lambda);
                        }
                        else
                        {
                            Model.OrderBy = source => source.OrderByDescending(Lambda);
                        }
                    }
                    else if (property.PropertyType == typeof(DateTime) ||
                        (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(property.PropertyType) == typeof(DateTime)))
                    {
                        var Lambda = Expression.Lambda<Func<T, DateTime>>(Expression.Convert(Expression.Property(param, property.Name), typeof(DateTime)), param);
                        if (defaultOrder == SortType.Ascending)
                        {
                            Model.OrderBy = source => source.OrderBy(Lambda);
                        }
                        else
                        {
                            Model.OrderBy = source => source.OrderByDescending(Lambda);
                        }
                    }
                    else if (property.PropertyType == typeof(bool) ||
                         (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>) && Nullable.GetUnderlyingType(property.PropertyType) == typeof(bool)))
                    {
                        var Lambda = Expression.Lambda<Func<T, bool>>(Expression.Convert(Expression.Property(param, property.Name), typeof(bool)), param);
                        if (defaultOrder == SortType.Ascending)
                        {
                            Model.OrderBy = source => source.OrderByDescending(Lambda);
                        }
                        else
                        {
                            Model.OrderBy = source => source.OrderBy(Lambda);
                        }
                    }
                    else if (property.PropertyType.IsEnum)
                    {
                        var Lambda = Expression.Lambda<Func<T, System.Enum>>(Expression.Convert(Expression.Property(param, property.Name), typeof(System.Enum)), param);
                        if (defaultOrder == SortType.Ascending)
                        {
                            Model.OrderBy = source => source.OrderByDescending(Lambda);
                        }
                        else
                        {
                            Model.OrderBy = source => source.OrderBy(Lambda);
                        }
                    }
                }
                else if (key.Contains("."))
                {
                    Type selectorResultType;
                    LambdaExpression selector = GenerateSelector<T>(key, out selectorResultType);
                    if (selector != null)
                    {
                        if (selectorResultType == typeof(string))
                        {
                            if (defaultOrder == SortType.Ascending)
                            {
                                Model.OrderBy = source => source.OrderBy((Expression<Func<T, string>>)selector);
                            }
                            else
                            {
                                Model.OrderBy = source => source.OrderByDescending((Expression<Func<T, string>>)selector);
                            }
                        }
                        else if (selectorResultType == typeof(Int64))
                        {
                            if (defaultOrder == SortType.Ascending)
                            {
                                Model.OrderBy = source => source.OrderBy((Expression<Func<T, Int64>>)selector);
                            }
                            else
                            {
                                Model.OrderBy = source => source.OrderByDescending((Expression<Func<T, Int64>>)selector);
                            }

                        }
                        else if (selectorResultType == typeof(double))
                        {
                            if (defaultOrder == SortType.Ascending)
                            {
                                Model.OrderBy = source => source.OrderBy((Expression<Func<T, Double>>)selector);
                            }
                            else
                            {
                                Model.OrderBy = source => source.OrderByDescending((Expression<Func<T, Double>>)selector);
                            }

                        }
                        else if (selectorResultType.ToString() == "System.Nullable`1[System.Double]")
                        {
                            if (defaultOrder == SortType.Ascending)
                            {
                                Model.OrderBy = source => source.OrderBy((Expression<Func<T, Double?>>)selector);
                            }
                            else
                            {
                                Model.OrderBy = source => source.OrderByDescending((Expression<Func<T, Double?>>)selector);
                            }

                        }
                        else if (selectorResultType == typeof(DateTime))
                        {
                            if (defaultOrder == SortType.Ascending)
                            {
                                Model.OrderBy = source => source.OrderBy((Expression<Func<T, DateTime>>)selector);
                            }
                            else
                            {
                                Model.OrderBy = source => source.OrderByDescending((Expression<Func<T, DateTime>>)selector);
                            }

                        }
                        else if (selectorResultType == typeof(bool))
                        {
                            if (defaultOrder == SortType.Ascending)
                            {
                                Model.OrderBy = source => source.OrderBy((Expression<Func<T, bool>>)selector);
                            }
                            else
                            {
                                Model.OrderBy = source => source.OrderByDescending((Expression<Func<T, bool>>)selector);
                            }

                        }
                        else if (selectorResultType.IsEnum)
                        {
                            if (defaultOrder == SortType.Ascending)
                            {
                                Model.OrderBy = source => source.OrderBy((Expression<Func<T, bool>>)selector);
                            }
                            else
                            {
                                Model.OrderBy = source => source.OrderByDescending((Expression<Func<T, bool>>)selector);
                            }
                        }
                    }
                }
            }
        }

        private static LambdaExpression GenerateSelector<T>(String propertyName, out Type resultType)
        {
            // Create a parameter to pass into the Lambda expression (x => x.OrderByField).
            var parameter = Expression.Parameter(typeof(T), "x");
            //  create the selector part, but support child properties
            PropertyInfo property;
            Expression propertyAccess;
            resultType = null;
            if (propertyName.Contains('.'))
            {
                // support to be sorted on child fields.
                String[] childProperties = propertyName.Split('.');
                property = typeof(T).GetProperty(childProperties[0]);
                if (property == null)
                {
                    return null;
                }
                propertyAccess = Expression.MakeMemberAccess(parameter, property);
                for (int i = 1; i < childProperties.Length; i++)
                {
                    property = property.PropertyType.GetProperty(childProperties[i]);
                    propertyAccess = Expression.MakeMemberAccess(propertyAccess, property);
                }
            }
            else
            {
                property = typeof(T).GetProperty(propertyName);
                if (property == null)
                {
                    return null;
                }
                propertyAccess = Expression.MakeMemberAccess(parameter, property);
            }
            resultType = property.PropertyType;
            // Create the order by expression.
            return Expression.Lambda(propertyAccess, parameter);
        }

        private static List<Expression<Func<T, object>>> GetChildRelation<T>(DataTransferObject<T> Model)
        {
            List<Expression<Func<T, object>>> result = new List<Expression<Func<T, object>>>();
            foreach (var property in Model.Result.GetType().GetProperties())
            {
                if (property.PropertyType.IsClass && !property.PropertyType.FullName.StartsWith("System."))
                    result.Add(Build<T>(property.Name));
            }
            return result;
        }

        public static void SetupLessThanEqualToFilters<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            Key = Key.Replace("filters[", "").Replace("]", "").Replace("<=", "");
            ParameterExpression parameter = Expression.Parameter(typeof(T), "m");

            Expression target = null;
            Expression exp = null;
            Type type = Model.Result.GetType();

            string[] Keys = null;
            Keys = new string[1] { Key };

            foreach (string key in Keys)
            {
                if (key != null && key.Contains("."))
                {
                    string[] properties = key.Split(new char[1] { '.' });

                    PropertyInfo propertyInfo = null;
                    Expression entityExpression = null;
                    Expression<Func<T, bool>> expressionForNull = x => true;

                    string excludedProperty = "";
                    foreach (string prop in properties)
                    {
                        if (excludedProperty == "")
                            excludedProperty = string.Concat(excludedProperty, prop);
                        else
                            excludedProperty = string.Concat(excludedProperty, "." + prop);

                        if (ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase))
                        {
                            propertyInfo = null;
                            break;
                        }

                        if (propertyInfo != null)
                        {
                            Type propertyType = propertyInfo.PropertyType;
                            propertyInfo = propertyType.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(entityExpression, prop);
                        }
                        else
                        {
                            propertyInfo = type.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(parameter, prop);
                        }

                        if (propertyInfo != null && !ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase) &&
                            propertyInfo.PropertyType.IsClass && !propertyInfo.PropertyType.FullName.StartsWith("System."))
                        {
                            var binaryExpressionForNullObject = Expression.NotEqual(entityExpression, Expression.Constant(null, propertyInfo.PropertyType));
                            var expressionForNullObject = Expression.Lambda<Func<T, bool>>(binaryExpressionForNullObject, parameter);

                            if (expressionForNull == null)
                                expressionForNull = expressionForNullObject;
                            else
                                expressionForNull = expressionForNull.And(expressionForNullObject);

                        }
                    }

                    if (propertyInfo == null)
                        return;

                    switch (entityExpression.Type.ToString())
                    {
                        case "System.DateTime":
                            {
                                target = (Value == "null") ?
                                 Expression.Constant(null, propertyInfo.PropertyType) :
                                 Expression.Constant(Convert.ToDateTime(Value));
                            }
                            break;
                        case "System.Int64":
                            {
                                target = (Value == "null") ?
                                  Expression.Constant(null, propertyInfo.PropertyType) :
                                  Expression.Constant(Convert.ToInt64(Value));
                            }
                            break;
                        case "System.Nullable`1[System.Double]":
                        case "System.Double":
                            {
                                target = (Value == "null") ?
                                  Expression.Constant(null, propertyInfo.PropertyType) :
                                  Expression.Constant(Convert.ToDouble(Value));
                            }
                            break;
                        default:
                            {
                                target = (Value == "null") ?
                                  Expression.Constant(null, entityExpression.Type) :
                                  Expression.Constant(Value);
                            }
                            break;
                    }
                    exp = Expression.LessThanOrEqual(entityExpression, Expression.Convert(target, propertyInfo.PropertyType));
                }
                else
                {
                    Expression property = Expression.Property(parameter, Key);
                    PropertyInfo filterProperty = type.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (filterProperty != null && Value != null && !ExludeProperties.Contains(filterProperty.Name, StringComparer.OrdinalIgnoreCase) && StringHelper.CanConvert(filterProperty.PropertyType, Value))
                    {
                        switch (property.Type.ToString())
                        {
                            case "System.DateTime":
                                {
                                    target = (Value == "null") ?
                                     Expression.Constant(null, property.Type) :
                                     Expression.Constant(Convert.ToDateTime(Value));
                                }
                                break;
                            case "System.Int64":
                                {
                                    target = (Value == "null") ?
                                      Expression.Constant(null, property.Type) :
                                      Expression.Constant(Convert.ToInt64(Value));
                                }
                                break;
                            default:
                                {
                                    target = (Value == "null") ?
                                      Expression.Constant(null, property.Type) :
                                      Expression.Constant(Value);
                                }
                                break;
                        }
                        exp = Expression.LessThanOrEqual(property, Expression.Convert(target, property.Type));
                    }
                }
            }


            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(exp, parameter);
            Model.Filter = Model.Filter.And(lambda);
        }

        public static void SetupGreaterThanEqualToFilters<T>(DataTransferObject<T> Model, string Key, string Value, List<string> ExludeProperties = null)
        {
            Key = Key.Replace("filters[", "").Replace("]", "").Replace(">=", "");
            ParameterExpression parameter = Expression.Parameter(typeof(T), "m");

            Expression target = null;
            Expression exp = null;
            Type type = Model.Result.GetType();

            string[] Keys = null;
            Keys = new string[1] { Key };

            foreach (string key in Keys)
            {
                if (key != null && key.Contains("."))
                {
                    string[] properties = key.Split(new char[1] { '.' });

                    PropertyInfo propertyInfo = null;
                    Expression entityExpression = null;
                    Expression<Func<T, bool>> expressionForNull = x => true;

                    string excludedProperty = "";
                    foreach (string prop in properties)
                    {
                        if (excludedProperty == "")
                            excludedProperty = string.Concat(excludedProperty, prop);
                        else
                            excludedProperty = string.Concat(excludedProperty, "." + prop);

                        if (ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase))
                        {
                            propertyInfo = null;
                            break;
                        }

                        if (propertyInfo != null)
                        {
                            Type propertyType = propertyInfo.PropertyType;
                            propertyInfo = propertyType.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(entityExpression, prop);
                        }
                        else
                        {
                            propertyInfo = type.GetProperty(prop, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                            if (propertyInfo != null)
                                entityExpression = Expression.Property(parameter, prop);
                        }

                        if (propertyInfo != null && !ExludeProperties.Contains(excludedProperty, StringComparer.OrdinalIgnoreCase) &&
                            propertyInfo.PropertyType.IsClass && !propertyInfo.PropertyType.FullName.StartsWith("System."))
                        {
                            var binaryExpressionForNullObject = Expression.NotEqual(entityExpression, Expression.Constant(null, propertyInfo.PropertyType));
                            var expressionForNullObject = Expression.Lambda<Func<T, bool>>(binaryExpressionForNullObject, parameter);

                            if (expressionForNull == null)
                                expressionForNull = expressionForNullObject;
                            else
                                expressionForNull = expressionForNull.And(expressionForNullObject);

                        }
                    }

                    if (propertyInfo == null)
                        return;

                    switch (entityExpression.Type.ToString())
                    {
                        case "System.DateTime":
                            {
                                target = (Value == "null") ?
                                 Expression.Constant(null, propertyInfo.PropertyType) :
                                 Expression.Constant(Convert.ToDateTime(Value));
                            }
                            break;
                        case "System.Int64":
                            {
                                target = (Value == "null") ?
                                  Expression.Constant(null, propertyInfo.PropertyType) :
                                  Expression.Constant(Convert.ToInt64(Value));
                            }
                            break;
                        case "System.Nullable`1[System.Double]":
                        case "System.Double":
                            {
                                target = (Value == "null") ?
                                  Expression.Constant(null, propertyInfo.PropertyType) :
                                  Expression.Constant(Convert.ToDouble(Value));
                            }
                            break;
                        default:
                            {
                                target = (Value == "null") ?
                                  Expression.Constant(null, entityExpression.Type) :
                                  Expression.Constant(Value);
                            }
                            break;
                    }
                    exp = Expression.GreaterThanOrEqual(entityExpression, Expression.Convert(target, propertyInfo.PropertyType));
                }
                else
                {
                    Expression property = Expression.Property(parameter, Key);
                    PropertyInfo filterProperty = type.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                    if (filterProperty != null && Value != null && !ExludeProperties.Contains(filterProperty.Name, StringComparer.OrdinalIgnoreCase) && StringHelper.CanConvert(filterProperty.PropertyType, Value))
                    {
                        switch (property.Type.ToString())
                        {
                            case "System.DateTime":
                                {
                                    target = (Value == "null") ?
                                     Expression.Constant(null, property.Type) :
                                     Expression.Constant(Convert.ToDateTime(Value));
                                }
                                break;
                            case "System.Int64":
                                {
                                    target = (Value == "null") ?
                                      Expression.Constant(null, property.Type) :
                                      Expression.Constant(Convert.ToInt64(Value));
                                }
                                break;
                            default:
                                {
                                    target = (Value == "null") ?
                                      Expression.Constant(null, property.Type) :
                                      Expression.Constant(Value);
                                }
                                break;
                        }
                        exp = Expression.GreaterThanOrEqual(property, Expression.Convert(target, property.Type));
                    }
                }
            }


            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(exp, parameter);
            Model.Filter = Model.Filter.And(lambda);
        }

        //will delete 
        public static void SetupLessThanEqualToFilterss<T>(DataTransferObject<T> Model, string key, string value, List<string> ExludeProperties = null)
        {
            key = key.Replace("filters[", "").Replace("]", "").Replace("<=", "");
            ParameterExpression parameter = Expression.Parameter(typeof(T), "m");
            Expression property = Expression.Property(parameter, key);
            Expression target = null;
            Expression exp = null;

            var switchType = property.Type.ToString();

            switch (switchType)
            {
                case "System.DateTime":
                    {
                        target = (value == "null") ?
                         Expression.Constant(null, property.Type) :
                         Expression.Constant(Convert.ToDateTime(value));
                    }
                    break;
                case "System.Int64":
                    {
                        target = (value == "null") ?
                          Expression.Constant(null, property.Type) :
                          Expression.Constant(Convert.ToInt64(value));
                    }
                    break;
                default:
                    {
                        target = (value == "null") ?
                          Expression.Constant(null, property.Type) :
                          Expression.Constant(value);
                    }
                    break;
            }

            exp = Expression.LessThanOrEqual(property, Expression.Convert(target, property.Type));

            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(exp, parameter);
            Model.Filter = Model.Filter.And(lambda);
        }
    }

    static class Extensions
    {
        public static Expression<Func<T, bool>> Filter<T>
            (this List<T> source, string columnName,
             string compValue)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
            Expression property = Expression.Property(parameter, columnName);
            Expression constant = Expression.Constant(compValue);
            Expression equality = Expression.Equal(property, constant);
            Expression<Func<T, bool>> predicate =
                Expression.Lambda<Func<T, bool>>(equality, parameter);

            return predicate;
        }
    }
}
