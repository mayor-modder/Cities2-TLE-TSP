using System;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;

namespace C2VM.TrafficLightsEnhancement.Utils
{
    public class EntityQueryUtils
    {
        public static EntityQuery GetEntityQuery(object obj, string fieldName)
        {
            if (!TryGetEntityQuery(obj, fieldName, out EntityQuery entityQuery, out string error))
            {
                throw new InvalidOperationException(error);
            }

            return entityQuery;
        }

        public static bool TryGetEntityQuery(object obj, string fieldName, out EntityQuery entityQuery, out string error)
        {
            entityQuery = default;
            if (!TryGetEntityQueryField(obj, fieldName, out FieldInfo fieldInfo, out error))
            {
                return false;
            }

            object value = fieldInfo.GetValue(obj);
            if (value is EntityQuery query)
            {
                entityQuery = query;
                return true;
            }

            error = $"{obj.GetType().FullName}.{fieldName} is not an {nameof(EntityQuery)} field.";
            return false;
        }

        public static void SetEntityQuery(object obj, string fieldName, EntityQuery entityQuery)
        {
            if (!TrySetEntityQuery(obj, fieldName, entityQuery, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public static bool TrySetEntityQuery(object obj, string fieldName, EntityQuery entityQuery, out string error)
        {
            if (!TryGetEntityQueryField(obj, fieldName, out FieldInfo fieldInfo, out error))
            {
                return false;
            }

            fieldInfo.SetValue(obj, entityQuery);
            return true;
        }

        public static void UpdateEntityQuery(SystemBase systemBase, string fieldName, NativeList<ComponentType> none)
        {
            if (!TryUpdateEntityQuery(systemBase, fieldName, none, out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public static bool TryUpdateEntityQuery(SystemBase systemBase, string fieldName, NativeList<ComponentType> none, out string error)
        {
            if (!TryGetEntityQuery(systemBase, fieldName, out EntityQuery query, out error))
            {
                return false;
            }

            try
            {
                using EntityQueryBuilder builder = GetEntityQueryBuilder(query, none);
                EntityQuery newQuery = builder.Build(systemBase);
                return TrySetEntityQuery(systemBase, fieldName, newQuery, out error);
            }
            catch (Exception ex)
            {
                error = $"Failed to update {systemBase.GetType().FullName}.{fieldName}: {ex.Message}";
                return false;
            }
        }

        public static EntityQueryBuilder GetEntityQueryBuilder(EntityQuery oldQuery, NativeList<ComponentType> none)
        {
            using var empty = new NativeList<ComponentType>(0, Allocator.Temp);
            return GetEntityQueryBuilder(oldQuery, empty, none, empty, empty, empty, empty);
        }

        public static EntityQueryBuilder GetEntityQueryBuilder
        (
            EntityQuery oldQuery,
            NativeList<ComponentType> any,
            NativeList<ComponentType> none,
            NativeList<ComponentType> all,
            NativeList<ComponentType> disabled,
            NativeList<ComponentType> absent,
            NativeList<ComponentType> present
        )
        {
            var builder = new EntityQueryBuilder(Allocator.Temp);
            var descArray = oldQuery.GetEntityQueryDescs();
            for (int i = 0; i < descArray.Length; i++)
            {
                EntityQueryDesc desc = descArray[i];
                var oldAny = CreateNativeList(desc.Any, Allocator.Temp);
                var oldNone = CreateNativeList(desc.None, Allocator.Temp);
                var oldAll = CreateNativeList(desc.All, Allocator.Temp);
                var oldDisabled = CreateNativeList(desc.Disabled, Allocator.Temp);
                var oldAbsent = CreateNativeList(desc.Absent, Allocator.Temp);
                var oldPresent = CreateNativeList(desc.Present, Allocator.Temp);
                try
                {
                    builder.WithAny(ref oldAny);
                    builder.WithNone(ref oldNone);
                    builder.WithAll(ref oldAll);
                    builder.WithDisabled(ref oldDisabled);
                    builder.WithAbsent(ref oldAbsent);
                    builder.WithPresent(ref oldPresent);
                    builder.WithAny(ref any);
                    builder.WithNone(ref none);
                    builder.WithAll(ref all);
                    builder.WithDisabled(ref disabled);
                    builder.WithAbsent(ref absent);
                    builder.WithPresent(ref present);
                    if (i < descArray.Length - 1)
                    {
                        builder.AddAdditionalQuery();
                    }
                }
                finally
                {
                    oldAny.Dispose();
                    oldNone.Dispose();
                    oldAll.Dispose();
                    oldDisabled.Dispose();
                    oldAbsent.Dispose();
                    oldPresent.Dispose();
                }
            }
            return builder;
        }

        public static NativeList<T> CreateNativeList<T>(T[] array, Allocator allocator) where T : unmanaged
        {
            var list = new NativeList<T>(array.Length, allocator);
            foreach (var item in array)
            {
                list.Add(item);
            }
            return list;
        }

        private static bool TryGetEntityQueryField(object obj, string fieldName, out FieldInfo fieldInfo, out string error)
        {
            fieldInfo = null;
            if (obj == null)
            {
                error = $"Cannot access {nameof(EntityQuery)} field '{fieldName}' on a null object.";
                return false;
            }

            fieldInfo = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
            {
                error = $"{obj.GetType().FullName} does not contain private instance field '{fieldName}'.";
                return false;
            }

            if (fieldInfo.FieldType != typeof(EntityQuery))
            {
                error = $"{obj.GetType().FullName}.{fieldName} has type {fieldInfo.FieldType.FullName}, expected {typeof(EntityQuery).FullName}.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
