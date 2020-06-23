using System.Collections.Generic;
namespace JTech.Tools
{
    public class BinaryHeap<T>
    {
        public T[] arr;
        public int Count;
        private Compare compare;
        public delegate bool Compare(T a, T b);

        public BinaryHeap(Compare compare)
        {
            arr = new T[0];
            this.compare = compare;
        }

        public BinaryHeap(List<T> list, Compare compare)
        {
            arr = new T[list.Count];
            this.compare = compare;
            for (int i = 0; i < list.Count; i++)
            {
                Push(list[i]);
            }
        }

        private void swap(int v1, int v2)
        {
            T temp = arr[v1];
            arr[v1] = arr[v2];
            arr[v2] = temp;
        }

        private void upHeap()
        {
            int now = Count - 1;
            while (now > 0)
            {
                if (compare(arr[now], arr[(now - 1) / 2]))
                {
                    swap(now, (now - 1) / 2);
                    now = (now - 1) / 2;
                }
                else
                {
                    break;
                }
            }
        }

        private void downHeap(int now)
        {
            if (now * 2 + 1 < Count)
            {
                if (compare(arr[now * 2 + 1], arr[now]))
                {
                    swap(now, now * 2 + 1);
                    downHeap(now * 2 + 1);
                }
                if (now * 2 + 2 < Count && compare(arr[now * 2 + 2], arr[now]))
                {
                    swap(now, now * 2 + 2);
                    downHeap(now * 2 + 2);
                }
            }
        }

        public T Peek()
        {
            return arr[0];
        }

        public T Pop()
        {
            T ret = arr[0];
            Count--;
            arr[0] = arr[Count];
            downHeap(0);
            return ret;
        }

        public void Push(T val)
        {
            if (arr.Length > Count)
            {
                arr[Count++] = val;
                upHeap();
            }
            else
            {
                T[] tempArr = new T[Count * 2 + 1];
                for (int i = 0; i < arr.Length; i++)
                {
                    tempArr[i] = arr[i];
                }
                arr = tempArr;
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
                if (arr[i].Equals(data))
                {
                    break;
                }
            }
            if (i == Count) return false;
            Count--;
            arr[i] = arr[Count];
            downHeap(i);
            return true;
        }

        public void Clear()
        {
            arr = new T[0];
            Count = 0;
        }
        
        public void FakeClear()
        {
            Count = 0;
        }
    }
}

