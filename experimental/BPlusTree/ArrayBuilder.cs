using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BPlusTree
{
    [DebuggerDisplay("Length = {Length}")]
    internal struct ArrayBuilder<T>
    {
        private T[]? _array;
        private int _maxLength;
        private int _length;

        public ArrayBuilder(int maxLength)
        {
            _array = null;
            _maxLength = maxLength;
            _length = 0;
        }

        public int Length => _length;
        
        public ref T this[int index]
        {
            get
            {
                Debug.Assert(index < _length);
                return ref _array![index];
            }
        }

        public ref T Last => ref this[_length - 1];

        public void Add(T item) => 
            (_array ??= new T[_maxLength])[_length++] = item;

        public void AddRange(ReadOnlySpan<T> items)
        {
            items.CopyTo((_array ??= new T[_maxLength]).AsSpan(start: _length));
            _length += items.Length;
        }

        public T[] MoveToArray()
        {
            if (_array is null)
            {
                return Array.Empty<T>();
            }

            T[] result;
            if (_length == _array.Length)
            {
                result = _array;
                _array = null;
            }
            else
            {
                result = new T[_length];
                _array.AsSpan(0, _length).CopyTo(result);
            }

            _length = 0;
            return result;
        }
    }
}
