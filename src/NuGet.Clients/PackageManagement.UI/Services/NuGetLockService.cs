// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// MEF component providing the lock which guarantees non-overlapping execution of NuGet operations.
    /// </summary>
    /// <remarks>
    /// Inspired by https://blogs.msdn.microsoft.com/pfxteam/2011/01/13/await-anything/
    /// </remarks>
    [Export(typeof(INuGetLockService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class NuGetLockService : INuGetLockService, IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public bool IsLockHeld => _semaphore.CurrentCount == 0;

        public IAsyncLockAwaitable AcquireLockAsync(CancellationToken token)
        {
            return new SemaphoreLockAwaiter(_semaphore, token);
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }

        // Custom awaiter which wraps the awaiter for a task awaiting the semaphore lock.
        // This awaiter implementation wraps a TaskAwaiter, and this implementation’s 
        // IsCompleted, OnCompleted, and GetResult members delegate to the contained TaskAwaiter’s.
        private class SemaphoreLockAwaiter : AsyncLockAwaiter, IAsyncLockAwaitable
        {
            private readonly TaskAwaiter _awaiter;
            private readonly SemaphoreSlim _semaphore;

            private bool _isReleased = false;

            public SemaphoreLockAwaiter(SemaphoreSlim semaphore, CancellationToken token)
            {
                if (semaphore == null)
                {
                    throw new ArgumentNullException(nameof(semaphore));
                }

                _semaphore = semaphore;
                _awaiter = _semaphore.WaitAsync(token).GetAwaiter();
            }

            public AsyncLockAwaiter GetAwaiter() => this;

            public override bool IsCompleted => _awaiter.IsCompleted;

            public override IDisposable GetResult()
            {
                _awaiter.GetResult();
                return new AsyncLockReleaser(this);
            }

            public override void OnCompleted(Action continuation) => _awaiter.OnCompleted(continuation);

            public override void Release()
            {
                if (!_isReleased)
                {
                    _isReleased = true;

                    _semaphore.Release();
                }
            }
        }
    }
}
