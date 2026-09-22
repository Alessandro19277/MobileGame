using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobileGame.Utilities
{
    /// <summary>
    /// Pool generico GC-friendly basato su Stack pre-allocato.
    /// Il warm-up avviene nel costruttore (chiamato in Awake): zero alloc a runtime.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly Stack<T>  _stack;
        private readonly Func<T>   _factory;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;

        public int CountInactive => _stack.Count;

        /// <param name="factory">Crea un nuovo oggetto (in Awake o se pool vuoto).</param>
        /// <param name="onGet">Attiva l'oggetto al momento del Get.</param>
        /// <param name="onRelease">Disattiva l'oggetto al momento del Release.</param>
        /// <param name="initialCapacity">Oggetti pre-allocati in Awake.</param>
        public ObjectPool(Func<T> factory, Action<T> onGet, Action<T> onRelease, int initialCapacity)
        {
            _factory   = factory;
            _onGet     = onGet;
            _onRelease = onRelease;
            _stack     = new Stack<T>(initialCapacity);

            // Warm-up: tutte le alloc avvengono qui, mai in Update
            for (int i = 0; i < initialCapacity; i++)
            {
                T obj = _factory();
                _onRelease(obj);
                _stack.Push(obj);
            }
        }

        /// <summary>Prende un oggetto dal pool (o ne crea uno nuovo se vuoto).</summary>
        public T Get()
        {
            T obj = _stack.Count > 0 ? _stack.Pop() : _factory();
            _onGet(obj);
            return obj;
        }

        /// <summary>Restituisce un oggetto al pool.</summary>
        public void Release(T obj)
        {
            _onRelease(obj);
            _stack.Push(obj);
        }
    }
}
