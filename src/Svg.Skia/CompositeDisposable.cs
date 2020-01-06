// Copyright (c) Wiesław Šoltés. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Svg.Skia
{
    public class CompositeDisposable : IDisposable
    {
        private List<IDisposable>? _disposables;

        public CompositeDisposable()
        {
            _disposables = new List<IDisposable>();
        }

        public void Add(IDisposable disposable)
        {
            if (_disposables != null)
            {
                _disposables.Add(disposable);
            }
        }

        public void Dispose()
        {
            if (_disposables != null)
            {
                foreach (var disposable in _disposables)
                {
                    disposable?.Dispose();
                }
            }
            _disposables = null;
        }
    }
}
