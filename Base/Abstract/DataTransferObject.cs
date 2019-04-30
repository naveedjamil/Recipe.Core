using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Recipe.NetCore.Base.Abstract
{
    public class DataTransferObject<T> : DtoBase
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
        private int Page_Number { get; set; }
        public int PageNumber
        {
            get { return this.Page_Number <= 0 ? 1 : this.Page_Number; }
            set { this.Page_Number = value; }
        }

        private int Page_Size { get; set; }
        public int PageSize
        {
            get { return this.Page_Size <= 0 ? 1 : this.Page_Size; }
            set { this.Page_Size = value; }
        }

        public int TotalPages { get { return (int)Math.Ceiling((double)this.TotalCount / this.PageSize); } }

        public string SortDirection { get; set; }

        public string OrderBy { get; set; }
    }
}
