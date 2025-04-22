// Copyright 2022-2025 Niantic.
using System;

namespace Niantic.Lightship.MagicLeap
{
    /// <summary>
    /// Implements a lazy singleton pattern.
    /// </summary>
    public abstract class LazySingleton<T> where T : class, new()
    {
        private static readonly Lazy<T> s_instance = new(() => new T());

        /// <summary>
        /// Acquires the singleton instance.
        /// </summary>
        public static T Instance => s_instance.Value;

        /// <summary>
        /// Indicates whether the singleton instance has been created.
        /// </summary>
        public static bool IsInitialized => s_instance.IsValueCreated;
    }
}
