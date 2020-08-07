/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Concurrent;
using System.Threading;
using NewRelic.Core.Logging;

namespace NewRelic.Collections
{
    public class ObjectPool<T> where T : IDisposable
    {
        private readonly System.Collections.Concurrent.ConcurrentBag<T> _pool = new System.Collections.Concurrent.ConcurrentBag<T>();
        private readonly Func<T> _factory;
        private long _capacity;

        private long _takes;
        private long _misses;
        private long _returns;
        private long _overflow;

        private readonly Timer _updateTimer;
        private readonly string _typeName;

        public ObjectPool(int initialCapacity, Func<T> factory)
        {
            _typeName = typeof(T).FullName;
            _factory = factory;
            _capacity = initialCapacity;

            _updateTimer = new Timer(new TimerCallback(_=>UpdateCapacity()), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        }

        private int Reclaim()
        {
            if (_pool.Count <= (_capacity * 1.2))
            {
                return 0;
            }

            var countToRemove = (_pool.Count - _capacity);

            int i = 0;
            for (i = 0; i < countToRemove; i++)
            {
                if (!_pool.TryTake(out _))
                {
                    break;
                }
            }

            return i;
        }

        private void UpdateCapacity()
        {
            var takes = Interlocked.Exchange(ref _takes, 0);
            var misses = Interlocked.Exchange(ref _misses, 0);
            var overflow = Interlocked.Exchange(ref _overflow, 0);
            var returns = Interlocked.Exchange(ref _returns, 0);

            var oldCapacity = _capacity;
            var proposedCapacity = (oldCapacity + takes) / 2;
            Interlocked.Exchange(ref _capacity, proposedCapacity);

            var reclaimCount = Reclaim();

            Log.Debug($"\r\nObject Pool Stats: {_typeName}\r\n{"takes/misses",-20}: {takes,10}{misses, 10}\r\n{"returns/overflow",-20}: {returns, 10}{overflow,10}\r\n{"reclaims",-20}: {reclaimCount,10}\r\n{"Capacity Change",-20}: {oldCapacity,10} --> {proposedCapacity}");
        }

        public T Take()
        {
            Interlocked.Increment(ref _takes);

            if (_pool.TryTake(out var result))
            {
                return result;
            }

            Interlocked.Increment(ref _misses);

            return _factory();
        }

        public bool Return(T item)
        {
            Interlocked.Increment(ref _returns);

            if (_pool.Count < _capacity)
            {
                _pool.Add(item);
                return true;
            }

            Interlocked.Increment(ref _overflow);

            return false;
        }
    }
}
