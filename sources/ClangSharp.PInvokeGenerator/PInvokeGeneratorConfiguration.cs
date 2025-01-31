// Copyright (c) .NET Foundation and Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClangSharp.Abstractions;

namespace ClangSharp
{
    public sealed class PInvokeGeneratorConfiguration
    {
        private const string DefaultMethodClassName = "Methods";

        private readonly SortedSet<string> _excludedNames;
        private readonly SortedSet<string> _forceRemappedNames;
        private readonly SortedSet<string> _includedNames;
        private readonly SortedSet<string> _traversalNames;
        private readonly SortedSet<string> _withSetLastErrors;
        private readonly SortedSet<string> _withSuppressGCTransitions;

        private readonly SortedDictionary<string, string> _remappedNames;
        private readonly SortedDictionary<string, AccessSpecifier> _withAccessSpecifiers;
        private readonly SortedDictionary<string, IReadOnlyList<string>> _withAttributes;
        private readonly SortedDictionary<string, string> _withCallConvs;
        private readonly SortedDictionary<string, string> _withClasses;
        private readonly SortedDictionary<string, string> _withLibraryPaths;
        private readonly SortedDictionary<string, string> _withNamespaces;
        private readonly SortedDictionary<string, (string, PInvokeGeneratorTransparentStructKind)> _withTransparentStructs;
        private readonly SortedDictionary<string, string> _withTypes;
        private readonly SortedDictionary<string, IReadOnlyList<string>> _withUsings;

        private PInvokeGeneratorConfigurationOptions _options;

        public PInvokeGeneratorConfiguration(string libraryPath, string namespaceName, string outputLocation, string testOutputLocation, PInvokeGeneratorOutputMode outputMode = PInvokeGeneratorOutputMode.CSharp, PInvokeGeneratorConfigurationOptions options = PInvokeGeneratorConfigurationOptions.None, IReadOnlyCollection<string> excludedNames = null, IReadOnlyCollection<string> includedNames = null, string headerFile = null, string methodClassName = null, string methodPrefixToStrip = null, IReadOnlyDictionary<string, string> remappedNames = null, IReadOnlyCollection<string> traversalNames = null, IReadOnlyDictionary<string, string> withAccessSpecifiers = null, IReadOnlyDictionary<string, IReadOnlyList<string>> withAttributes = null, IReadOnlyDictionary<string, string> withCallConvs = null, IReadOnlyDictionary<string, string> withClasses = null, IReadOnlyDictionary<string, string> withLibraryPaths = null, IReadOnlyDictionary<string, string> withNamespaces = null, IReadOnlyCollection<string> withSetLastErrors = null, IReadOnlyCollection<string> withSuppressGCTransitions = null, IReadOnlyDictionary<string, (string, PInvokeGeneratorTransparentStructKind)> withTransparentStructs = null, IReadOnlyDictionary<string, string> withTypes = null, IReadOnlyDictionary<string, IReadOnlyList<string>> withUsings = null)
        {
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                libraryPath = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(methodClassName))
            {
                methodClassName = DefaultMethodClassName;
            }

            if (string.IsNullOrWhiteSpace(methodPrefixToStrip))
            {
                methodPrefixToStrip = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            if (outputMode is not PInvokeGeneratorOutputMode.CSharp and not PInvokeGeneratorOutputMode.Xml)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            if (outputMode == PInvokeGeneratorOutputMode.Xml &&
                (options & PInvokeGeneratorConfigurationOptions.GenerateMultipleFiles) == 0 &&
                ((options & PInvokeGeneratorConfigurationOptions.GenerateTestsNUnit) != 0 ||
                 (options & PInvokeGeneratorConfigurationOptions.GenerateTestsXUnit) != 0))
            {
                // we can't mix XML and C#! we're in XML mode, not generating multiple files, and generating tests; fail
                throw new ArgumentException("Can't generate tests in XML mode without multiple files.",
                    nameof(options));
            }

            if (options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateCompatibleCode) && options.HasFlag(PInvokeGeneratorConfigurationOptions.GeneratePreviewCode))
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(outputLocation))
            {
                throw new ArgumentNullException(nameof(outputLocation));
            }

            _options = options;

            _excludedNames = new SortedSet<string>();
            _forceRemappedNames = new SortedSet<string>();
            _includedNames = new SortedSet<string>();
            _traversalNames = new SortedSet<string>();
            _withSetLastErrors = new SortedSet<string>();
            _withSuppressGCTransitions = new SortedSet<string>();

            _remappedNames = new SortedDictionary<string, string>();
            _withAccessSpecifiers = new SortedDictionary<string, AccessSpecifier>();
            _withAttributes = new SortedDictionary<string, IReadOnlyList<string>>();
            _withCallConvs = new SortedDictionary<string, string>();
            _withClasses = new SortedDictionary<string, string>();
            _withLibraryPaths = new SortedDictionary<string, string>();
            _withNamespaces = new SortedDictionary<string, string>();
            _withTransparentStructs = new SortedDictionary<string, (string, PInvokeGeneratorTransparentStructKind)>();
            _withTypes = new SortedDictionary<string, string>();
            _withUsings = new SortedDictionary<string, IReadOnlyList<string>>();

            HeaderText = string.IsNullOrWhiteSpace(headerFile) ? string.Empty : File.ReadAllText(headerFile);
            LibraryPath = $@"""{libraryPath}""";
            DefaultClass = methodClassName;
            MethodPrefixToStrip = methodPrefixToStrip;
            DefaultNamespace = namespaceName;
            OutputMode = outputMode;
            OutputLocation = Path.GetFullPath(outputLocation);
            TestOutputLocation = !string.IsNullOrWhiteSpace(testOutputLocation) ? Path.GetFullPath(testOutputLocation) : string.Empty;

            if (!_options.HasFlag(PInvokeGeneratorConfigurationOptions.NoDefaultRemappings))
            {
                if (!ExcludeNIntCodegen)
                {
                    _remappedNames.Add("intptr_t", "nint");
                    _remappedNames.Add("ptrdiff_t", "nint");
                    _remappedNames.Add("size_t", "nuint");
                    _remappedNames.Add("uintptr_t", "nuint");
                }
                else
                {
                    _remappedNames.Add("intptr_t", "IntPtr");
                    _remappedNames.Add("ptrdiff_t", "IntPtr");
                    _remappedNames.Add("size_t", "UIntPtr");
                    _remappedNames.Add("uintptr_t", "UIntPtr");
                }
            }

            AddRange(_excludedNames, excludedNames);
            AddRange(_forceRemappedNames, remappedNames, ValueStartsWithAt);
            AddRange(_includedNames, includedNames);
            AddRange(_traversalNames, traversalNames, NormalizePathSeparators);
            AddRange(_withSetLastErrors, withSetLastErrors);
            AddRange(_withSuppressGCTransitions, withSuppressGCTransitions);

            AddRange(_remappedNames, remappedNames, RemoveAtPrefix);
            AddRange(_withAccessSpecifiers, withAccessSpecifiers, ConvertStringToAccessSpecifier);
            AddRange(_withAttributes, withAttributes);
            AddRange(_withCallConvs, withCallConvs);
            AddRange(_withClasses, withClasses);
            AddRange(_withLibraryPaths, withLibraryPaths);
            AddRange(_withNamespaces, withNamespaces);
            AddRange(_withTransparentStructs, withTransparentStructs, RemoveAtPrefix);
            AddRange(_withTypes, withTypes);
            AddRange(_withUsings, withUsings);
        }

        public bool DontUseUsingStaticsForEnums => _options.HasFlag(PInvokeGeneratorConfigurationOptions.DontUseUsingStaticsForEnums);

        public bool ExcludeComProxies => _options.HasFlag(PInvokeGeneratorConfigurationOptions.ExcludeComProxies);

        public bool ExcludeEmptyRecords => _options.HasFlag(PInvokeGeneratorConfigurationOptions.ExcludeEmptyRecords);

        public bool ExcludeEnumOperators => _options.HasFlag(PInvokeGeneratorConfigurationOptions.ExcludeEnumOperators);

        public IReadOnlyCollection<string> ExcludedNames => _excludedNames;

        public IReadOnlyCollection<string> IncludedNames => _includedNames;

        public bool GenerateAggressiveInlining => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateAggressiveInlining);

        public bool GenerateCompatibleCode => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateCompatibleCode);

        public bool GenerateDocIncludes => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateDocIncludes);

        public bool GenerateExplicitVtbls => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateExplicitVtbls);

        public bool GenerateFileScopedNamespaces => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateFileScopedNamespaces);

        public bool GenerateMacroBindings => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateMacroBindings);

        public bool GenerateMarkerInterfaces => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateMarkerInterfaces);

        public bool ExcludeFnptrCodegen
        {
            get
            {
                return GenerateCompatibleCode || _options.HasFlag(PInvokeGeneratorConfigurationOptions.ExcludeFnptrCodegen);
            }

            set
            {
                if (value)
                {
                    _options |= PInvokeGeneratorConfigurationOptions.ExcludeFnptrCodegen;
                }
                else
                {
                    _options &= ~PInvokeGeneratorConfigurationOptions.ExcludeFnptrCodegen;
                }
            }
        }

        public bool ExcludeNIntCodegen => GenerateCompatibleCode || _options.HasFlag(PInvokeGeneratorConfigurationOptions.ExcludeNIntCodegen);

        public bool GenerateMultipleFiles => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateMultipleFiles);

        public bool GenerateTestsNUnit => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateTestsNUnit);

        public bool GenerateTestsXUnit => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateTestsXUnit);

        public bool GenerateTrimmableVtbls => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateTrimmableVtbls);

        public bool GenerateUnixTypes => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateUnixTypes);

        public string HeaderText { get; }

        public string LibraryPath { get;}

        public bool LogExclusions => _options.HasFlag(PInvokeGeneratorConfigurationOptions.LogExclusions);

        public bool LogVisitedFiles => _options.HasFlag(PInvokeGeneratorConfigurationOptions.LogVisitedFiles);

        public bool ExcludeFunctionsWithBody => _options.HasFlag(PInvokeGeneratorConfigurationOptions.ExcludeFunctionsWithBody);

        public bool ExcludeAnonymousFieldHelpers => _options.HasFlag(PInvokeGeneratorConfigurationOptions.ExcludeAnonymousFieldHelpers);

        public bool LogPotentialTypedefRemappings => _options.HasFlag(PInvokeGeneratorConfigurationOptions.LogPotentialTypedefRemappings);

        public bool GenerateCppAttributes => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateCppAttributes);

        public bool GenerateHelperTypes => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateHelperTypes);

        public bool GenerateNativeInheritanceAttribute => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateNativeInheritanceAttribute);

        public bool GenerateSetsLastSystemErrorAttribute => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateSetsLastSystemErrorAttribute);

        public bool GenerateTemplateBindings => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateTemplateBindings);

        public bool GenerateUnmanagedConstants => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateUnmanagedConstants);

        public bool GenerateVtblIndexAttribute => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateVtblIndexAttribute);

        public bool GenerateSourceLocationAttribute => _options.HasFlag(PInvokeGeneratorConfigurationOptions.GenerateSourceLocationAttribute);

        public string DefaultClass { get; }

        public string MethodPrefixToStrip { get;}

        public string DefaultNamespace { get; }

        public PInvokeGeneratorOutputMode OutputMode { get; }

        public string OutputLocation { get; }

        public IReadOnlyDictionary<string, string> RemappedNames => _remappedNames;

        public IReadOnlyCollection<string> ForceRemappedNames => _forceRemappedNames;

        public string TestOutputLocation { get; }

        public IReadOnlyCollection<string> TraversalNames => _traversalNames;

        public IReadOnlyDictionary<string, AccessSpecifier> WithAccessSpcifier => _withAccessSpecifiers;

        public IReadOnlyDictionary<string, IReadOnlyList<string>> WithAttributes => _withAttributes;

        public IReadOnlyDictionary<string, string> WithCallConvs => _withCallConvs;

        public IReadOnlyDictionary<string, string> WithClasses => _withClasses;

        public IReadOnlyDictionary<string, string> WithLibraryPaths => _withLibraryPaths;

        public IReadOnlyDictionary<string, string> WithNamespaces => _withNamespaces;

        public IReadOnlyCollection<string> WithSetLastErrors => _withSetLastErrors;

        public IReadOnlyCollection<string> WithSuppressGCTransitions => _withSuppressGCTransitions;

        public IReadOnlyDictionary<string, (string Name, PInvokeGeneratorTransparentStructKind Kind)> WithTransparentStructs => _withTransparentStructs;

        public IReadOnlyDictionary<string, string> WithTypes => _withTypes;

        public IReadOnlyDictionary<string, IReadOnlyList<string>> WithUsings => _withUsings;

        private static void AddRange<TValue>(SortedDictionary<string, TValue> dictionary, IEnumerable<KeyValuePair<string, TValue>> keyValuePairs)
        {
            if (keyValuePairs != null)
            {
                foreach (var keyValuePair in keyValuePairs)
                {
                    // Use the indexer, rather than Add, so that any
                    // default mappings can be overwritten if desired.
                    dictionary[keyValuePair.Key] = keyValuePair.Value;
                }
            }
        }

        private static void AddRange<TInput, TValue>(SortedDictionary<string, TValue> dictionary, IEnumerable<KeyValuePair<string, TInput>> keyValuePairs, Func<TInput, TValue> convert)
        {
            if (keyValuePairs != null)
            {
                foreach (var keyValuePair in keyValuePairs)
                {
                    // Use the indexer, rather than Add, so that any
                    // default mappings can be overwritten if desired.
                    dictionary[keyValuePair.Key] = convert(keyValuePair.Value);
                }
            }
        }

        private static void AddRange<TInput>(SortedSet<TInput> hashSet, IEnumerable<TInput> keys)
        {
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    _ = hashSet.Add(key);
                }
            }
        }

        private static void AddRange<TInput, TValue>(SortedSet<TValue> hashSet, IEnumerable<TInput> keys, Func<TInput, TValue> convert)
        {
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    _ = hashSet.Add(convert(key));
                }
            }
        }

        private static void AddRange<TInput>(SortedSet<string> hashSet, IEnumerable<KeyValuePair<string, TInput>> keyValuePairs, Func<TInput, bool> shouldAdd)
        {
            if (keyValuePairs != null)
            {
                foreach (var keyValuePair in keyValuePairs)
                {
                    if (shouldAdd(keyValuePair.Value))
                    {
                        _ = hashSet.Add(keyValuePair.Key);
                    }
                }
            }
        }

        private static AccessSpecifier ConvertStringToAccessSpecifier(string input)
        {
            if (input.Equals("internal", StringComparison.OrdinalIgnoreCase))
            {
                return AccessSpecifier.Internal;
            }
            else if (input.Equals("private", StringComparison.OrdinalIgnoreCase))
            {
                return AccessSpecifier.Private;
            }
            else if (input.Equals("private protected", StringComparison.OrdinalIgnoreCase))
            {
                return AccessSpecifier.PrivateProtected;
            }
            else if (input.Equals("protected", StringComparison.OrdinalIgnoreCase))
            {
                return AccessSpecifier.Protected;
            }
            else if (input.Equals("protected internal", StringComparison.OrdinalIgnoreCase))
            {
                return AccessSpecifier.ProtectedInternal;
            }
            else if (input.Equals("public", StringComparison.OrdinalIgnoreCase))
            {
                return AccessSpecifier.Public;
            }
            else
            {
                return AccessSpecifier.None;
            }
        }

        private static string NormalizePathSeparators(string value) => value.Replace('\\', '/');

        private static string RemoveAtPrefix(string value) => ValueStartsWithAt(value) ? value[1..] : value;

        private static (string, PInvokeGeneratorTransparentStructKind) RemoveAtPrefix((string Name, PInvokeGeneratorTransparentStructKind Kind) value) => (ValueStartsWithAt(value.Name) ? value.Name[1..] : value.Name, value.Kind);

        private static bool ValueStartsWithAt(string value) => value.StartsWith("@");
    }
}
