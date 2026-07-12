using System;
using System.Collections.Generic;
using System.Linq;

namespace SmearFramework
{
    public static class StageRegistry
    {
        private static Type[] _cached; // built once per domain

        // user-facing IPipelineStage types only -- internal sub-stages are tagged [InternalStage] and skipped
        public static IReadOnlyList<Type> GetStageTypes()
        {
            if (_cached != null) return _cached;

            var iface = typeof(IPipelineStage);
            _cached = iface.Assembly.GetTypes()
                .Where(t => iface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Where(t => t.GetCustomAttributes(typeof(InternalStageAttribute), false).Length == 0)
                .OrderBy(t => t.Name)
                .ToArray();

            return _cached;
        }

        // every concrete IPipelineStage including internals -- used by tests and integration code
        public static IReadOnlyList<Type> GetAllStageTypes()
        {
            var iface = typeof(IPipelineStage);
            return iface.Assembly.GetTypes()
                .Where(t => iface.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .OrderBy(t => t.Name)
                .ToArray();
        }

        // parameterless ctor; caller should pass a type returned from GetStageTypes
        public static IPipelineStage Instantiate(Type t)
        {
            return (IPipelineStage)Activator.CreateInstance(t);
        }
    }
}
