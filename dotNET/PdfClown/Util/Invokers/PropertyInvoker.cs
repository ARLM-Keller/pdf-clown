using System;
using System.Linq.Expressions;
using System.Reflection;

namespace PdfClown.Util.Reflection
{
    public class PropertyInvoker<T, V> : ActionInvoker<T, V>
    {
        public PropertyInvoker(PropertyInfo info)
            : base(info.Name, GetExpressionGet(info), info.CanWrite ? GetExpressionSet(info) : null)
        { }

        public PropertyInvoker(string name)
            : this(typeof(T).GetProperty(name))
        { }

        //https://www.codeproject.com/Articles/584720/Expression-Based-Property-Getters-and-Setters
        public static Func<T, V> GetExpressionGet(PropertyInfo info)
        {
            var param = Expression.Parameter(typeof(T), "target");
            var property = Expression.Property(param, info);

            return Expression.Lambda<Func<T, V>>(property, param).Compile();
        }

        public static Action<T, V> GetExpressionSet(PropertyInfo info)
        {
            var param = Expression.Parameter(typeof(T), "target");
            var value = Expression.Parameter(typeof(V), "value");
            var proeprty = Expression.Property(param, info);

            return Expression.Lambda<Action<T, V>>(Expression.Assign(proeprty, value), param, value).Compile();
        }
    }

}
