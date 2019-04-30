using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Recipe.NetCore.Model
{
    public class ValidationResultModel
    {
        public string Status { get; set; }

        public string Message { get; }

        public List<ValidationError> Errors { get; }

        public ValidationResultModel(ModelStateDictionary modelState)
        {
            this.Message = "Validation Failed";
            this.Errors = modelState.Keys
                    .SelectMany(key => modelState[key].Errors.Select(x => new ValidationError(key, x.ErrorMessage)))
                    .ToList();
        }

        public ValidationResultModel(ExceptionContext ex)
        {
            this.Status = "Failed";

            var listOfExceptionMessage = new List<ValidationError>();
            ExceptionContext currentException = ex;
            while (currentException.Exception != null)
            {
                listOfExceptionMessage.Add(new ValidationError("", currentException.Exception.Message));
                currentException.Exception = currentException.Exception.InnerException;
            }
            this.Errors = listOfExceptionMessage;
        }

        public ValidationResultModel(Exception ex)
        {
            this.Message = "Failed";

            var listOfExceptionMessage = new List<ValidationError>();
            Exception currentException = ex;
            while (currentException != null)
            {
                listOfExceptionMessage.Add(new ValidationError("", currentException.Message));
                currentException = currentException.InnerException;
            }
            this.Errors = listOfExceptionMessage;
        }
    }
}
