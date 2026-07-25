using System;
using System.Collections.Generic;

namespace ContextStage
{
    /// <summary>
    /// 고정 크기 카드 묶음을 여러 개 미리 유지하는 런타임 덱.
    /// 현재 묶음의 마지막 장을 뽑는 즉시 다음 묶음을 승격하고 새 대기 묶음을 만든다.
    /// </summary>
    public sealed class PreparedDeck<T> where T : class
    {
        sealed class Batch
        {
            readonly List<T> _items;
            int _nextIndex;

            public Batch(List<T> items) => _items = items;
            public int Remaining => Math.Max(0, _items.Count - _nextIndex);
            public T Draw() => Remaining > 0 ? _items[_nextIndex++] : null;
        }

        readonly int _preparedBatchCount;
        readonly Func<List<T>> _buildBatch;
        readonly Queue<Batch> _batches = new Queue<Batch>();

        public PreparedDeck(int batchSize, int preparedBatchCount, Func<T> pickOne)
            : this(
                preparedBatchCount,
                CreatePickerBatchFactory(batchSize, pickOne))
        {
        }

        public PreparedDeck(int preparedBatchCount, Func<List<T>> buildBatch)
        {
            _preparedBatchCount = Math.Max(2, preparedBatchCount);
            _buildBatch = buildBatch ?? throw new ArgumentNullException(nameof(buildBatch));
        }

        public int PreparedBatchCount => _batches.Count;
        public int CurrentRemaining => _batches.Count > 0 ? _batches.Peek().Remaining : 0;

        public void Reset()
        {
            _batches.Clear();
            FillPreparedBatches();
        }

        public T Draw()
        {
            EnsureCurrentBatch();
            if (_batches.Count == 0) return null;

            var current = _batches.Peek();
            T item = current.Draw();
            if (current.Remaining == 0) PromoteAndReplenish();
            return item;
        }

        void EnsureCurrentBatch()
        {
            while (_batches.Count > 0 && _batches.Peek().Remaining == 0)
                PromoteAndReplenish();

            if (_batches.Count == 0) FillPreparedBatches();
        }

        void PromoteAndReplenish()
        {
            if (_batches.Count > 0 && _batches.Peek().Remaining == 0)
                _batches.Dequeue();

            FillPreparedBatches();
        }

        void FillPreparedBatches()
        {
            while (_batches.Count < _preparedBatchCount)
            {
                var batch = BuildBatch();
                if (batch == null) break;
                _batches.Enqueue(batch);
            }
        }

        Batch BuildBatch()
        {
            List<T> items = _buildBatch();
            return items != null && items.Count > 0 ? new Batch(items) : null;
        }

        static Func<List<T>> CreatePickerBatchFactory(int batchSize, Func<T> pickOne)
        {
            if (pickOne == null) throw new ArgumentNullException(nameof(pickOne));

            int safeBatchSize = Math.Max(1, batchSize);
            return () =>
            {
                var items = new List<T>(safeBatchSize);
                for (int i = 0; i < safeBatchSize; i++)
                {
                    T item = pickOne();
                    if (item == null) break;
                    items.Add(item);
                }

                return items;
            };
        }
    }
}
