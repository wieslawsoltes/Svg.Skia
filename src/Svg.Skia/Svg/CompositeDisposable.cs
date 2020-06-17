using System;
using System.Collections.Generic;

namespace Svg.Skia
{
#if USE_PICTURE
    internal sealed class CompositeDisposable : IDisposable
    {
        public CompositeDisposable()
        {
        }

        public void Add(object disposable)
        {
        }

        public void Dispose()
        {
        }
    }
#else
    internal sealed class CompositeDisposable1 : IDisposable
    {
        private readonly List<IDisposable> _disposables;

        public CompositeDisposable1()
        {
            _disposables = new List<IDisposable>();
        }

        public void Add(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
#endif
}
