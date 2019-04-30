using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Recipe.NetCore.Helper
{
    public class ObjectHelper
    {
        protected ObjectHelper() { }

        /// <summary>
        /// Copy an object to destination object, only matching fields will be copied
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sourceObject">An object with matching fields of the destination object</param>
        /// <param name="destObject">Destination object, must already be created</param>
        public static void CopyObject<T>(object sourceObject, ref T destObject)
        {
            //If either the source, or destination is null, return
            if (sourceObject == null || destObject == null)
            { return; }

            //Get the type of each object
            Type sourceType = sourceObject.GetType();
            Type targetType = destObject.GetType();
            var lstType = sourceObject as IList;

            if (lstType != null)
            {
                IList listSourceObject = (IList)sourceObject;
                IList listTargetObject = (IList)Activator.CreateInstance(targetType);

                foreach (var item in listSourceObject)
                {
                    var targetListItemObject = Activator.CreateInstance(listTargetObject.GetType().GenericTypeArguments[0]);
                    CopyObject(item, ref targetListItemObject);
                    listTargetObject.Add(targetListItemObject);
                }
                destObject = (T)listTargetObject;
                return;
            }

            //Loop through the source properties
            foreach (PropertyInfo p in sourceType.GetProperties())
            {
                //Get the matching property in the destination object
                PropertyInfo targetObj = targetType.GetProperty(p.Name);

                //If there is none, skip
                if (targetObj == null)
                {
                    MappingAttribute attr = (MappingAttribute)p.GetCustomAttributes(typeof(MappingAttribute), true).FirstOrDefault();

                    if (attr != null)
                    { targetObj = targetType.GetProperty(attr.Name); }

                    if (targetObj == null)
                    { continue; }
                }

                object val = p.GetValue(sourceObject, null);

                if (!targetObj.CanWrite)
                { continue; }

                //Set the value in the destination
                if (val != null && val.GetType().IsClass && !val.GetType().FullName.StartsWith("System."))
                {
                    var targetClassObject = Activator.CreateInstance(targetObj.PropertyType);
                    CopyObject(val, ref targetClassObject);
                    targetObj.SetValue(destObject, targetClassObject);
                }
                else
                {
                    if (val is IList || (val != null && val.GetType().FullName.StartsWith("System.Collections.Generic.HashSet")))
                    {
                        var listType = targetObj.PropertyType.GenericTypeArguments;

                        if (listType != null && listType.Length > 0 && !listType[0].FullName.StartsWith("System.") &&
                                val.GetType().FullName.StartsWith("System.Collections.Generic.HashSet"))
                        {
                            Type elementType = listType[0];
                            Type repositoryType = typeof(List<>).MakeGenericType(elementType);
                            var targetClassObject = (ICollection)Activator.CreateInstance(repositoryType);

                            var result = ((IEnumerable)val).Cast<object>().ToList();
                            CopyObject(result, ref targetClassObject);
                            targetObj.SetValue(destObject, targetClassObject);
                        }
                        else if (listType != null && listType.Length > 0 && !listType[0].FullName.StartsWith("System."))
                        {
                            Type elementType = listType[0];
                            Type repositoryType = typeof(List<>).MakeGenericType(elementType);
                            var targetClassObject = (ICollection)Activator.CreateInstance(repositoryType);
                            CopyObject(val, ref targetClassObject);
                            targetObj.SetValue(destObject, targetClassObject);
                        }
                        else
                        {
                            var targetClassObject = Activator.CreateInstance(targetObj.PropertyType);
                            CopyObject(val, ref targetClassObject);
                            targetObj.SetValue(destObject, targetClassObject);
                        }
                    }
                    else
                    {
                        targetObj.SetValue(destObject, ChangeType(val, targetObj.PropertyType), null);
                    }
                }
            }
        }

        public static object ChangeType(object value, Type conversion)
        {
            var t = conversion;

            if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                if (value == null)
                {
                    return null;
                }
                t = Nullable.GetUnderlyingType(t);
            }

            return Convert.ChangeType(value, t);
        }

        public static List<T> CopyObjects<T>(IList sourceObjects) where T : new()
        {
            List<T> result = new List<T>();

            if (sourceObjects != null)
            {
                foreach (object sourceObject in sourceObjects)
                { result.Add(CopyObject<T>(sourceObject)); } 
            }

            return result;
        }

        public static T CopyObject<T>(object sourceObject) where T : new()
        {
            T destObject = new T();

            //If either the source, or destination is null, return
            if (sourceObject == null)
            { return default(T); }

            CopyObject(sourceObject, ref destObject);
            return destObject;
        }

        public static object CreateGeneric(Type generic, Type innerType, params object[] args)
        {
            try
            {
                Type specificType = generic.MakeGenericType(new Type[] { innerType });
                return Activator.CreateInstance(specificType, args);
            }
            catch (Exception)
            {
                //in the case of excption, return null

            }
            return null;
        }

        public static void CopyIEnumerableToIListObject(IEnumerable sourceObject, ref IList destObject)
        {
            foreach (var item in sourceObject)
            {
                destObject.Add(item);
            }
        }

        public static ListComparisonResult<T> CompareLists<T>(IList newList, IList existingList, List<string> propertyNames, Type returnType)
        {
            ListComparisonResult<T> result = new ListComparisonResult<T>();
            if (existingList == null || existingList.Count == 0)
            {
                result.NewItems = (IList)GetListOfGivenType(newList, returnType);
            }
            else
            {
                result.NewItems = GetItemsNotPresentInSource<T>(existingList, newList, propertyNames, returnType, ref result);
            }

            if (newList == null || newList.Count == 0)
            {
                result.DeletedItems = (IList)GetListOfGivenType(existingList, returnType);
            }
            else
            {
                ListComparisonResult<T> nullObj = null;
                result.DeletedItems = GetItemsNotPresentInSource<T>(newList, existingList, propertyNames, returnType, ref nullObj);
            }

            if (result.CommonItems.Count == 0)
            { result.CommonItems = (IList)Activator.CreateInstance(returnType); }

            return result;
        }

        private static object GetListOfGivenType(IList list, Type returnType)
        {
            object obj = Activator.CreateInstance(returnType);

            foreach (var item in list)
            { ((IList)obj).Add(item); }

            return obj;
        }

        public static List<List<T>> BreakListIntoMultipleLists<T>(IList<T> list, int chunkSize)
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentException("chunkSize must be greater than 0.");
            }

            List<List<T>> retVal = new List<List<T>>();

            int index = 0;
            while (index != list.Count)
            {

                int count = (list.Count - index) > chunkSize ? chunkSize : (list.Count - index);
                List<T> chunkedList = new List<T>();
                index = index + count;
                while (count != 0)
                {
                    chunkedList.Add(list[index - count]);
                    count--;
                }
                retVal.Add(chunkedList);

            }

            return retVal;
        }

        private static IList GetItemsNotPresentInSource<T>(IList listToCompare, IList source, List<string> propertyNames, Type returnType, ref ListComparisonResult<T> result)
        {
            object returnObj = Activator.CreateInstance(returnType);
            object commonListObject = Activator.CreateInstance(returnType);
            if (source != null)
            {
                PropertyInfo[] properties = typeof(T).GetProperties();
                foreach (var item in source)
                {
                    List<object> sourceProperties = new List<object>();

                    //add all comparison properties to a list
                    foreach (string propertyName in propertyNames)
                    {
                        PropertyInfo propertyInfo = properties.FirstOrDefault(x => x.Name == propertyName);
                        if (propertyInfo != null)
                        {
                            object sourceValue = propertyInfo.GetValue(item, null);
                            sourceProperties.Add(sourceValue);
                        }
                    }

                    if (listToCompare != null)
                    {
                        // compare with each element in the other list
                        if (!ContainsSameItem(sourceProperties, listToCompare, propertyNames))
                        {
                            ((IList)returnObj).Add((T)item);
                        }
                        else
                        {
                            if (result != null)
                            { ((IList)commonListObject).Add((T)item); }
                        }
                    }
                }
            }

            if (result != null)
            { result.CommonItems = (IList)commonListObject; }

            return (IList)returnObj;
        }

        private static bool ContainsSameItem(List<object> sourceProperties, IList listToCompare, List<string> propertyNames)
        {

            bool isSameItem = false;

            foreach (var item in listToCompare)
            {
                List<object> listProperties = new List<object>();
                PropertyInfo[] properties = item.GetType().GetProperties();
                foreach (string propertyName in propertyNames)
                {

                    PropertyInfo propertyInfo = properties.FirstOrDefault(x => x.Name == propertyName);
                    object value = propertyInfo.GetValue(item, null);
                    listProperties.Add(value);
                }

                if (ArePropertiesSame(sourceProperties, listProperties))
                {
                    isSameItem = true;
                    break;
                }
            }
            return isSameItem;
        }

        private static bool ArePropertiesSame(IList list1, IList list2)
        {
            bool isEqual = true;
            for (int index = 0; index < list1.Count; index++)
            {
                var listType = list2[index] as IList;
                if (listType != null)
                {
                    isEqual = CompareListTypeValue((IList)list2[index], (IList)list1[index]);
                }
                else
                {
                    if (list2[index] == null || list1[index] == null)
                    { isEqual = false; }
                    else if (!list2[index].Equals(list1[index]))
                    { isEqual = false; }
                }

                if (!isEqual)
                { break; }
            }
            return isEqual;
        }

        private static bool CompareListTypeValue(IList list1, IList list2)
        {
            if (list1 == null && list2 == null)
            { return true; }
            if ((list1 != null && list1.Count == 0) && (list2 != null && list2.Count == 0))
            { return true; }

            bool isEqual = false;
            List<string> propertyNames = new List<string>();

            if (list2 != null && list2.Count > 0)
            {
                List<object> sourceProperties = new List<object>();
                PropertyInfo[] properties = list2[0].GetType().GetProperties();
                foreach (PropertyInfo propertyInfo in properties)
                {
                    object value = propertyInfo.GetValue(list2[0], null);
                    sourceProperties.Add(value);
                    propertyNames.Add(propertyInfo.Name);
                }
                isEqual = ContainsSameItem(sourceProperties, list1, propertyNames);
            }

            return isEqual;
        }

        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", "source");
            }

            //Don't serialize a null object, simply return the default for that object
            if (Object.ReferenceEquals(source, null))
            {
                return default(T);
            }

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                formatter.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(stream);
            }
        }

        public static byte[] ObjectToByteArray(Object obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public static string ToLongDate(DateTime? datevalue)
        {
            if (datevalue == null)
            { return ""; }
            else
            { return datevalue.Value.ToLongDateString(); }
        }

        public static string ToShortDate(DateTime? datevalue)
        {
            if (datevalue == null)
            { return ""; }
            else
            { return datevalue.Value.ToShortDateString(); }
        }

        public static string Serialize<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return json;
        }

        public static T Deserialize<T>(string obj)
        {
            var json = JsonConvert.DeserializeObject<T>(obj);
            return json;
        }
        public static dynamic DeserializeDynamic(string obj)
        {
            return JsonConvert.DeserializeObject(obj);
        }
    }
    public class ListComparisonResult<T>
    {
        public IList NewItems { get; set; }
        public IList DeletedItems { get; set; }
        public IList CommonItems { get; set; }

        public ListComparisonResult()
        {
            this.NewItems = new List<T>();
            this.DeletedItems = new List<T>();
            this.CommonItems = new List<T>();
        }
    }
}
