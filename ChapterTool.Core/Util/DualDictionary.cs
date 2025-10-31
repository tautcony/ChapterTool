using System.Collections.Generic;

namespace ChapterTool.Util
{
    public class DualDictionary<T1, T2>
    {
        private readonly Dictionary<T1, T2> _dataS2I = new Dictionary<T1, T2>();
        private readonly Dictionary<T2, T1> _dataI2S = new Dictionary<T2, T1>();

        public T1 this[T2 index] => _dataI2S[index];
        public T2 this[T1 type] => _dataS2I[type];

        public void Bind(T2 id, T1 type)
        {
            _dataI2S[id] = type;
            _dataS2I[type] = id;
        }
        public void Bind(T1 type, T2 id)
        {
            _dataI2S[id] = type;
            _dataS2I[type] = id;
        }
    }
}
