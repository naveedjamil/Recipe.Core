using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using TMH.Common.Helper;

namespace Recipe.NetCore.Base.Abstract
{
    public class DataTransferObject<T> : DTOBase
    {
        public DataTransferObject() { }
        public DataTransferObject(T result)
        {
            this.Result = result;
        }
        public DataTransferObject(T result, Paging paging)
        {
            this.Result = result;
            this.Paging = paging;
        }
        public DataTransferObject(T result, Paging paging, List<Expression<Func<T, object>>> includes)
        {
            this.Result = result;
            this.Paging = paging;
            this.Includes = includes;
        }

        public T Result { get; set; }
        public Paging Paging { get; set; }
        [JsonIgnore]
        public Expression<Func<T, bool>> Filter { get; set; }
        [JsonIgnore]
        public Func<IQueryable<T>, IOrderedQueryable<T>> OrderBy { get; set; }
        [JsonIgnore]
        public List<Expression<Func<T, object>>> Includes { get; set; }
        
    }

    public class Paging
    {
        public long TotalCount { get; set; }
        private int _PageNumber { get; set; }
        public int PageNumber { get { return this._PageNumber <= 0 ? 1 : this._PageNumber; } set { this._PageNumber = value; } }

        private int _PageSize { get; set; }
        public int PageSize { get { return this._PageSize <= 0 ? 1 : this._PageSize; } set { this._PageSize = value; } }

        public int TotalPages { get { return (int)Math.Ceiling((double)this.TotalCount / this.PageSize); } }

        public string SortDirection { get; set; }

        public string OrderBy { get; set; }
    }
}
