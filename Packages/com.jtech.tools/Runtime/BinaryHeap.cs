using System.Collections.Generic;
namespace JTech.Tools
{
     public class BinaryHeap<T>
    {
        public T[] arr;
        public int Count;
        private Compare compare;
        private OnIndexChange onIndexChange;
        public delegate bool Compare(T a, T b);
        public delegate void OnIndexChange(T a, int index);

        public BinaryHeap(Compare compare)
        {
            arr = new T[0];
            this.compare = compare;
        }
        public BinaryHeap(int count, Compare compare)
        {
            arr = new T[count];
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
        public void SetOnIndexChange(OnIndexChange onIndexChange)
        {
            this.onIndexChange = onIndexChange;
        }

        private void swap(int v1, int v2)
        {
            T temp = arr[v1];
            arr[v1] = arr[v2];
            arr[v2] = temp;
            if (onIndexChange != null)
            {
                onIndexChange(arr[v1], v1);
                onIndexChange(arr[v2], v2);
            }
        }

        private int upHeap(int now)
        {
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
            return now;
        }

        private int downHeap(int now)
        {
            while (now * 2 + 1 < Count)
            {
                var c21 = now * 2 + 2 < Count && compare(arr[now * 2 + 2], arr[now * 2 + 1]);
                if (c21 && compare(arr[now * 2 + 2], arr[now]))
                {
                    swap(now, now * 2 + 2);
                    now = now * 2 + 2;
                }
                else if (compare(arr[now * 2 + 1], arr[now]))
                {
                    swap(now, now * 2 + 1);
                    now = now * 2 + 1;
                }
                else
                {
                    break;
                }
            }
            return now;
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

        public int Push(T val)
        {
            if (arr.Length > Count)
            {
                arr[Count++] = val;
                return upHeap(Count - 1);
            }
            else
            {
                T[] tempArr = new T[Count * 2 + 1];
                for (int i = 0; i < arr.Length; i++)
                {
                    tempArr[i] = arr[i];
                }
                arr = tempArr;
                return Push(val);
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
        public int Update(T data, bool isUp)
        {
            var i = 0;
            for (; i < Count; i++)
            {
                if (arr[i].Equals(data))
                {
                    break;
                }
            }
            if (i == Count) return -1;
            if (isUp)
                return upHeap(i);
            else
                return downHeap(i);
        }

        public int Update(int index, bool isUp)
        {
            if (isUp)
                return upHeap(index);
            else
                return downHeap(index);
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

