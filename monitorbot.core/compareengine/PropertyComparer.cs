using System;
using System.Linq.Expressions;
using monitorbot.core.bot;

namespace monitorbot.core.compareengine
{
    public class PropertyComparer<T> {
        public readonly Func<Update<T>, bool> Compare;
        public readonly Func<Update<T>, Response> Describe;

        public PropertyComparer(Func<Update<T>, bool> comparer, Func<Update<T>, Response> describer)
        {
            Compare = comparer;
            Describe = describer;
        }

        public static PropertyComparer<T> BasicEqualityComparer<TProp>(Expression<Func<Update<T>, TProp>> property, Func<TProp, TProp, string> describer)
        {
            return null; // TODO
        }
    }
}