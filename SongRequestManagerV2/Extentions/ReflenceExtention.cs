using System;
using System.Reflection;

namespace SongRequestManagerV2.Extentions
{
    public static class ReflenceExtention
    {
        /// <summary>
        /// Invokes a method from <typeparamref name="T" /> on an object.
        /// </summary>
        /// <typeparam name="U">the type that the method returns</typeparam>
        /// <typeparam name="T">the type to search for the method on</typeparam>
        /// <param name="obj">the object instance</param>
        /// <param name="methodName">the method's name</param>
        /// <param name="args">the method arguments</param>
        /// <returns>the return value</returns>
        /// <exception cref="T:System.MissingMethodException">if <paramref name="methodName" /> does not exist on <typeparamref name="T" /></exception>
        public static void InvokeMethod<T>(this T obj, string methodName, params object[] args)
        {
            var method = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) {
                throw new MissingMethodException("Method " + methodName + " does not exist", "methodName");
            }
            method?.Invoke(obj, args);
        }
    }
}
