using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    /// <summary>
    /// The framework with domain reload off (#190, G8). With Enter Play Mode Options set to skip the
    /// domain reload, Unity keeps every static field as the last Play session left it, so each one
    /// that can change must be reset as Play mode starts, by a
    /// <c>[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]</c> method
    /// in its own type, or be listed in <see cref="Kept"/> with why it needs none. Ported from
    /// GreyBoxVillage's test of its own statics (#178).
    /// </summary>
    /// <remarks>
    /// Only the runtime assembly is checked: it is what runs in Play mode. The Editor assembly's
    /// statics are editor caches, such as the UI kit's style sheet, which Play mode has no reason
    /// to reset.
    /// </remarks>
    public sealed class DomainReloadTests
    {
        private static readonly Assembly Framework = typeof(Agent).Assembly;

        /// <summary>
        /// Static state that is right without a reset as Play mode starts, by full field name, with
        /// why. Where something else clears it, <see cref="KeptField.Check"/> proves that it does,
        /// returning null, or says what is wrong.
        /// </summary>
        private static readonly Dictionary<string, KeptField> Kept = new Dictionary<string, KeptField>
        {
            ["Agenerela.Providers.ProviderConfig.names"] = new KeptField(
                "Keyed weakly by the provider instance, and right for whichever provider it names. A new Play " +
                "session builds new providers (ProviderConfig.ForgetAll), so it never reads an entry from the " +
                "last; clearing it would leave a provider still in use without its config's name in telemetry."),
            ["Agenerela.RequestScheduler.schedulers"] = new KeptField(
                "Keyed weakly by the provider instance, like ProviderConfig.names. Clearing it would give a " +
                "provider still in use a second queue, two requests at once on one backend."),
            ["Agenerela.Providers.ProviderConfig.Built"] = new KeptField(
                "Editor only. ProviderConfig.ForgetAll empties it as Play mode starts (ExitingEditMode, before " +
                "SubsystemRegistration) and as it ends. A second reset at SubsystemRegistration could drop a " +
                "provider a game built in its own SubsystemRegistration method, and split its config in two.",
                BuiltIsEmptiedByForgetAll),
        };

        [Test]
        public void EveryResetTakesNoParameters()
        {
            var resets = Resets();

            Assert.That(resets.Select(Name), Has.Some.EqualTo("Agenerela.AgenerelaSettings.ResetForPlaySession"),
                "The settings' reset must be found, or this test checks nothing.");
            foreach (var reset in resets)
            {
                Assert.That(reset.GetParameters(), Is.Empty, Name(reset) + " must take no parameters, or Unity never calls it.");
                Assert.That(reset.DeclaringType.ContainsGenericParameters, Is.False, Name(reset) + " is in a generic type, where Unity never calls it.");
            }
        }

        [Test]
        public void EveryStaticFieldIsResetAsPlayModeStarts()
        {
            var resets = Resets();
            var fields = MutableStatics().ToList();
            Assert.That(fields.Select(Name), Has.Some.EqualTo("Agenerela.AgenerelaSettings.Override"),
                "The settings' Override must be found, or this test checks nothing.");

            // Every reset is run for every field, so put back what each field held, the ones a reset
            // clears as a side effect included.
            var saved = fields.Where(f => !f.IsInitOnly).ToDictionary(f => f, f => f.GetValue(null));
            var problems = new List<string>();
            try
            {
                foreach (var field in fields)
                {
                    string name = Name(field);
                    if (Kept.TryGetValue(name, out var kept))
                    {
                        string broken = kept.Check?.Invoke();
                        if (broken != null)
                        {
                            problems.Add(name + " is kept because something else clears it, but " + broken);
                        }

                        continue;
                    }

                    if (field.DeclaringType.ContainsGenericParameters)
                    {
                        problems.Add(name + " is static state in a generic type, which this test cannot reach. Move it, or list it in Kept with why.");
                        continue;
                    }

                    var check = Dirty(field);
                    if (check == null)
                    {
                        // A reset in its type proves nothing about this field, so the test will not guess.
                        problems.Add(name + " is static state this test cannot put a value in, so it cannot tell whether " +
                                     "anything resets it. List it in Kept with why it needs no reset, and a Check if " +
                                     "something else clears it.");
                        continue;
                    }

                    try
                    {
                        foreach (var reset in resets)
                        {
                            reset.Invoke(null, null);
                        }

                        if (check.StillDirty())
                        {
                            problems.Add(name + " keeps its value from the last Play session: reset it in a " +
                                         "[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] method, " +
                                         "or list it in Kept with why it needs none.");
                        }
                    }
                    finally
                    {
                        check.Undo();
                    }
                }
            }
            finally
            {
                foreach (var pair in saved)
                {
                    pair.Key.SetValue(null, pair.Value);
                }
            }

            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void CheckingBuiltLeavesEveryOtherConfigItsProvider()
        {
            var other = ScriptableObject.CreateInstance<FakeProviderConfig>();
            try
            {
                var provider = other.Provider;

                Assert.That(BuiltIsEmptiedByForgetAll(), Is.Null);

                Assert.That(other.Provider, Is.SameAs(provider), "An agent on it would otherwise share a config with a second provider.");
                Assert.That(other.Builds, Is.EqualTo(1));
            }
            finally
            {
                ProviderConfig.ForgetAll();
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void EveryFieldKeptOnPurposeIsStillStaticState()
        {
            // So the list cannot outlive the fields it excuses.
            var names = MutableStatics().Select(Name).ToList();

            Assert.That(Kept.Keys.Where(k => !names.Contains(k)), Is.Empty,
                "Kept lists fields that are no longer mutable static state; remove them.");
        }

        // The reset methods Unity calls first as Play mode starts.
        private static List<MethodInfo> Resets()
        {
            return Framework.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>()?.loadType == RuntimeInitializeLoadType.SubsystemRegistration)
                .ToList();
        }

        /// <summary>
        /// Every static field that can change at run time: not a constant, and not readonly unless it
        /// holds a collection that can grow. A static auto-property's or a field-like event's backing
        /// field counts; a compiler's own caches, such as a lambda's, do not.
        /// </summary>
        private static IEnumerable<FieldInfo> MutableStatics()
        {
            return Framework.GetTypes()
                .Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() == null && !t.Name.StartsWith("<", StringComparison.Ordinal))
                .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(f => !f.IsLiteral && (!f.IsInitOnly || IsGrowable(f)));
        }

        // A collection that can be added to, judged by what the field holds rather than how it is
        // declared: an IReadOnlyList holding a List can still grow. Weak tables count; arrays and
        // read-only wrappers do not.
        private static bool IsGrowable(FieldInfo field)
        {
            var type = field.DeclaringType.ContainsGenericParameters ? field.FieldType : field.GetValue(null)?.GetType() ?? field.FieldType;
            if (type.IsArray || IsGeneric(type, typeof(ReadOnlyCollection<>)) || IsGeneric(type, typeof(ReadOnlyDictionary<,>)))
            {
                return false;
            }

            return typeof(ICollection).IsAssignableFrom(type)
                   || IsGeneric(type, typeof(ConditionalWeakTable<,>))
                   || type.GetInterfaces().Any(i => IsGeneric(i, typeof(ICollection<>)));
        }

        private static bool IsGeneric(Type type, Type definition)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == definition;
        }

        /// <summary>
        /// Puts a value in the field that a reset must undo, and returns how to tell whether it did and
        /// how to clean up after. Null when the type has no value to offer.
        /// </summary>
        private static Dirtied Dirty(FieldInfo field)
        {
            var type = field.FieldType;
            object current = field.GetValue(null);

            if (field.IsInitOnly)
            {
                return current == null ? null : DirtyCollection(current);
            }

            object dirty;
            Action destroy = null;
            if (type == typeof(bool))
            {
                dirty = !(bool)(current ?? false);
            }
            else if (type == typeof(string))
            {
                dirty = (current as string ?? "") + "dirty";
            }
            else if (type.IsEnum)
            {
                dirty = Enum.ToObject(type, Convert.ToInt64(current) + 1);
            }
            else if (type.IsPrimitive)
            {
                dirty = Convert.ChangeType(Convert.ToDouble(current) + 1, type);
            }
            else if (typeof(Delegate).IsAssignableFrom(type))
            {
                var invoke = type.GetMethod("Invoke");
                var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
                Expression body = invoke.ReturnType == typeof(void) ? Expression.Empty() : (Expression)Expression.Default(invoke.ReturnType);
                dirty = Expression.Lambda(type, body, parameters).Compile();
            }
            else if (typeof(ScriptableObject).IsAssignableFrom(type) && !type.IsAbstract)
            {
                // Such as the project's settings, found once and kept.
                var made = ScriptableObject.CreateInstance(type);
                made.hideFlags = HideFlags.HideAndDontSave;
                dirty = made;
                destroy = () => Object.DestroyImmediate(made);
            }
            else if (type.IsClass && !type.IsAbstract && !typeof(Object).IsAssignableFrom(type))
            {
                dirty = RuntimeHelpers.GetUninitializedObject(type);
            }
            else
            {
                return null;
            }

            field.SetValue(null, dirty);
            return new Dirtied(
                () =>
                {
                    object after = field.GetValue(null);
                    return ReferenceEquals(after, dirty) || Equals(after, dirty);
                },
                () => destroy?.Invoke());
        }

        /// <summary>
        /// Gives a readonly collection an element of the test's own, which a reset must remove: through
        /// a two-argument <c>Add</c> or <c>TryAdd</c> for a dictionary, or a one-argument <c>Add</c>,
        /// <c>Enqueue</c> or <c>Push</c> for the rest. Null when it has none, or when no element can
        /// be made for it, such as a key of an interface type.
        /// </summary>
        private static Dirtied DirtyCollection(object collection)
        {
            var methods = collection.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            var pair = methods.FirstOrDefault(m => (m.Name == "Add" || m.Name == "TryAdd") && m.GetParameters().Length == 2);
            if (pair != null)
            {
                var types = pair.GetParameters().Select(p => p.ParameterType).ToArray();
                var containsKey = methods.FirstOrDefault(m => m.Name == "ContainsKey" && m.GetParameters().Length == 1);
                if (containsKey == null || !TrySample(types[0], out object key) || key == null || !TrySample(types[1], out object value))
                {
                    return null;
                }

                var removeKey = methods.FirstOrDefault(m => m.Name == "Remove" && m.GetParameters().Length == 1);
                pair.Invoke(collection, new[] { key, value });
                return new Dirtied(
                    () => (bool)containsKey.Invoke(collection, new[] { key }),
                    () => removeKey?.Invoke(collection, new[] { key }));
            }

            var single = methods.FirstOrDefault(m => (m.Name == "Add" || m.Name == "Enqueue" || m.Name == "Push") && m.GetParameters().Length == 1);
            if (single == null || !(collection is IEnumerable items) || !TrySample(single.GetParameters()[0].ParameterType, out object element))
            {
                return null;
            }

            single.Invoke(collection, new[] { element });
            var remove = methods.FirstOrDefault(m => m.Name == "Remove" && m.GetParameters().Length == 1);
            return new Dirtied(
                () => items.Cast<object>().Any(e => ReferenceEquals(e, element) || Equals(e, element)),
                () => remove?.Invoke(collection, new[] { element }));
        }

        // A value of this type that no other code holds, where one can be made: a fresh string, a
        // default struct, an uninitialised object. False for an interface, an abstract type or a
        // Unity object.
        private static bool TrySample(Type type, out object sample)
        {
            sample = null;
            if (type == typeof(string))
            {
                sample = "dirty " + Guid.NewGuid().ToString("N");
                return true;
            }

            if (type.IsValueType)
            {
                sample = Activator.CreateInstance(type);
                return true;
            }

            if (type.IsClass && !type.IsAbstract && !typeof(Object).IsAssignableFrom(type) && !type.ContainsGenericParameters)
            {
                sample = RuntimeHelpers.GetUninitializedObject(type);
                return true;
            }

            return false;
        }

        // Null when ProviderConfig.ForgetAll empties Built and the config builds again; otherwise what went wrong.
        private static string BuiltIsEmptiedByForgetAll()
        {
            var built = (HashSet<ProviderConfig>)typeof(ProviderConfig).GetField("Built", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var providerField = typeof(ProviderConfig).GetField("provider", BindingFlags.Instance | BindingFlags.NonPublic);

            // ForgetAll drops every config's provider, so keep the ones built before this check and put
            // them back after it: an agent still on one must not find its config building a second.
            // A list, not a dictionary: two destroyed Unity objects compare equal.
            var before = built.Select(c => (Config: c, Provider: providerField.GetValue(c))).ToList();
            var config = ScriptableObject.CreateInstance<FakeProviderConfig>();
            try
            {
                var first = config.Provider;
                if (!built.Contains(config))
                {
                    return "building a provider did not record its config, so this checks nothing.";
                }

                ProviderConfig.ForgetAll();

                if (built.Count > 0)
                {
                    return "ProviderConfig.ForgetAll left it holding " + built.Count + " config(s).";
                }

                return ReferenceEquals(config.Provider, first) ? "the config kept its provider after ForgetAll." : null;
            }
            finally
            {
                ProviderConfig.ForgetAll();
                Object.DestroyImmediate(config);
                foreach (var (kept, provider) in before)
                {
                    providerField.SetValue(kept, provider);
                    built.Add(kept);
                }
            }
        }

        private static string Name(MemberInfo member)
        {
            return member.DeclaringType.FullName + "." + member.Name;
        }

        private sealed class KeptField
        {
            public KeptField(string why, Func<string> check = null)
            {
                Why = why;
                Check = check;
            }

            public string Why { get; }

            public Func<string> Check { get; }
        }

        private sealed class Dirtied
        {
            private readonly Func<bool> stillDirty;
            private readonly Action undo;

            public Dirtied(Func<bool> stillDirty, Action undo)
            {
                this.stillDirty = stillDirty;
                this.undo = undo;
            }

            public bool StillDirty()
            {
                return stillDirty();
            }

            public void Undo()
            {
                undo();
            }
        }
    }
}
