using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace PdfClown.Util.Reflection
{
    public abstract class Invoker : IInvoker
    {
        private static readonly Dictionary<Type, Dictionary<string, IInvoker>> cache = new Dictionary<Type, Dictionary<string, IInvoker>>();
        public static IInvoker GetPropertyInvoker(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName);

            if (!cache.TryGetValue(property.DeclaringType, out var properties))
            {
                cache[property.DeclaringType] =
                    properties = new Dictionary<string, IInvoker>(StringComparer.Ordinal);
            }

            if (!properties.TryGetValue(propertyName, out var invoker))
            {
                var invokerType = typeof(PropertyInvoker<,>).MakeGenericType(property.DeclaringType, property.PropertyType);
                properties[propertyName] =
                    invoker = (IInvoker)Activator.CreateInstance(invokerType, property);
            }
            return invoker;
        }

        public string Name { get; set; }

        public Type DataType { get; set; }

        public Type TargetType { get; set; }

        public abstract bool CanWrite { get; }


        public abstract object GetValue(object target);

        public abstract void SetValue(object target, object value);
    }

    public abstract class Invoker<T, V> : Invoker, IInvoker<T, V>
    {
        public Invoker()
        {
            DataType = typeof(V);
            TargetType = typeof(T);
        }

        public abstract V GetValue(T target);

        public override object GetValue(object target) => GetValue((T)target);

        public abstract void SetValue(T target, V value);

        public void SetValue(object target, V value)
        {
            SetValue((T)target, value);
        }

        public override void SetValue(object target, object value) => SetValue((T)target, (V)value);

        public override string ToString()
        {
            return $"{TargetType.Name}.{Name} {DataType.Name}";
        }
    }
}
