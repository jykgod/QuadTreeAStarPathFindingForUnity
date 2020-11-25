using System.Collections.Generic;
namespace JTech.Tools
{
    public class BinaryHeap<T>
    {
        private T[] _arr;
        public int Count;
        private Compare compare;
        public delegate bool Compare(T a, T b);

        public BinaryHeap(Compare compare)
        {
            _arr = new T[0];
            this.compare = compare;
        }

        public BinaryHeap(List<T> list, Compare compare)
        {
            _arr = new T[list.Count];
            this.compare = compare;
            for (int i = 0; i < list.Count; i++)
            {
                Push(list[i]);
            }
        }

        private void Swap(int v1, int v2)
        {
            T temp = _arr[v1];
            _arr[v1] = _arr[v2];
            _arr[v2] = temp;
        }

        private void UpHeap()
        {
            int now = Count - 1;
            while (now > 0)
            {
                if (compare(_arr[now], _arr[(now - 1) / 2]))
                {
                    Swap(now, (now - 1) / 2);
                    now = (now - 1) / 2;
                }
                else
                {
                    break;
                }
            }
        }

        private void DownHeap(int now)
        {
            while (true)
            {
                if (now * 2 + 1 < Count)
                {
                    if (compare(_arr[now * 2 + 1], _arr[now]))
                    {
                        if (now * 2 + 2 < Count && compare(_arr[now * 2 + 2], _arr[now * 2 + 1]))
                        {
                            Swap(now, now * 2 + 2);
                            now = now * 2 + 2;
                            continue;
                        }

                        Swap(now, now * 2 + 1);
                        DownHeap(now * 2 + 1);
                    }
                    if (now * 2 + 2 >= Count || !compare(_arr[now * 2 + 2], _arr[now])) return;
                    Swap(now, now * 2 + 2);
                    now = now * 2 + 2;
                    continue;
                }

                break;
            }
        }

        public T Peek()
        {
            return _arr[0];
        }

        public T Pop()
        {
            T ret = _arr[0];
            Count--;
            _arr[0] = _arr[Count];
            DownHeap(0);
            return ret;
        }

        public void Push(T val)
        {
            if (_arr.Length > Count)
            {
                _arr[Count++] = val;
                UpHeap();
            }
            else
            {
                T[] tempArr = new T[Count * 2 + 1];
                for (int i = 0; i < _arr.Length; i++)
                {
                    tempArr[i] = _arr[i];
                }
                _arr = tempArr;
                Push(val);
            }
        }

        public bool Empty()
        {
            return Count == 0;
        }

        public bool Remove(T data)
        {
            var i = 0;
            for (; i < Count; i++)
            {
                if (_arr[i].Equals(data))
                {
                    break;
                }
            }
            if (i == Count) return false;
            Count--;
            _arr[i] = _arr[Count];
            DownHeap(i);
            return true;
        }

        public void Clear()
        {
            _arr = new T[0];
            Count = 0;
        }
        
        public void FakeClear()
        {
            Count = 0;
        }
    }
}

