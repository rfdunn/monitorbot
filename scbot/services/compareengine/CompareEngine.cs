﻿using System;
using System.Collections.Generic;
using System.Linq;
using scbot.bot;
using scbot.services.persistence;
using scbot.utils;

namespace scbot.services.compareengine
{
    class CompareEngine<T>
    {
        private ListPersistenceApi<Tracked<T>> m_Persistence;
        private const string c_PersistenceKey = "tracked-tc-builds";
        private readonly Func<T, string> m_PrefixStringGenerator;
        private readonly List<PropertyComparer<T>> m_Comparers;

        public CompareEngine(ListPersistenceApi<Tracked<T>> persistence, Func<T, string> prefixString, IEnumerable<PropertyComparer<T>> comparers)
        {
            m_Persistence = persistence;
            m_PrefixStringGenerator = prefixString;
            m_Comparers = new List<PropertyComparer<T>>(comparers);
        }

        internal IEnumerable<ComparisonResult<T>> CompareBuildStates(IEnumerable<Update<T>> comparison)
        {
            var differences = comparison.Select(x => new
            {
                differences = GetDifferenceString(x),
                update = x
            }).ToList();

            differences = differences.Where(x => x.differences != null).ToList();

            return differences.Select(x => new ComparisonResult<T>(
                new Response(x.differences.Message, x.update.Channel, x.differences.Image), 
                x.update.NewValue));
        }

        private Response GetDifferenceString(Update<T> ttc)
        {
            var differences = GetDifferences(ttc).ToList();
            if (differences.Any())
            {
                var image = differences.Select(x => x.Image).FirstOrDefault(x => x.IsNotDefault());
                return new Response(string.Format("{0} {1}",
                    m_PrefixStringGenerator(ttc.NewValue), string.Join(", ", differences.Select(x => x.Message))), null, image);
            }
            return null;
        }

        private IEnumerable<Response> GetDifferences(Update<T> x)
        {
            foreach (var comparer in m_Comparers)
            {
                if (comparer.Compare(x))
                {
                    yield return comparer.Describe(x);
                }
            }
        }
    }
}
