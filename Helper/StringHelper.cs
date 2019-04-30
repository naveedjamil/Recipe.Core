using System;
using System.ComponentModel;

namespace Recipe.NetCore.Helper
{
    public static class StringHelper
    {
        public static bool CanConvert(Type type, string value)
        {
            if (string.IsNullOrEmpty(value) || type == null)
            { return false; }

            TypeConverter conv = TypeDescriptor.GetConverter(type);

            if (conv.CanConvertFrom(typeof(string)))
            {
                try
                {
                    conv.ConvertFrom(value);
                    return true;
                }
                catch (Exception)
                {
                    // in the case of exception
                }
            }
            return false;
        }
    }
}
