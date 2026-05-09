using UnityEngine;

namespace FourPersonExpeditions.UI.Pagination
{
    internal sealed class PaginationState
    {
        private readonly int _itemsPerPage;
        private int _itemCount;
        private int _currentPage;

        public PaginationState(int itemsPerPage)
        {
            _itemsPerPage = Mathf.Max(1, itemsPerPage);
        }

        public int ItemsPerPage
        {
            get { return _itemsPerPage; }
        }

        public int ItemCount
        {
            get { return _itemCount; }
        }

        public int CurrentPage
        {
            get { return _currentPage; }
        }

        public int MaxPages
        {
            get { return PaginationMath.GetMaxPages(_itemCount, _itemsPerPage); }
        }

        public int StartIndex
        {
            get { return _currentPage * _itemsPerPage; }
        }

        public int EndIndex
        {
            get { return Mathf.Min(StartIndex + _itemsPerPage, _itemCount); }
        }

        public bool HasNextPage
        {
            get { return _currentPage < MaxPages - 1; }
        }

        public void Reset(int itemCount)
        {
            _itemCount = Mathf.Max(0, itemCount);
            _currentPage = 0;
        }

        public void SetItemCount(int itemCount)
        {
            _itemCount = Mathf.Max(0, itemCount);
            _currentPage = PaginationMath.ClampPage(_currentPage, _itemCount, _itemsPerPage);
        }

        public bool Move(int delta)
        {
            int nextPage = PaginationMath.ClampPage(_currentPage + delta, _itemCount, _itemsPerPage);
            if (nextPage == _currentPage)
            {
                return false;
            }

            _currentPage = nextPage;
            return true;
        }
    }

    internal static class PaginationMath
    {
        public static int GetMaxPages(int itemCount, int itemsPerPage)
        {
            int safeItemsPerPage = Mathf.Max(1, itemsPerPage);
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0, itemCount) / (float)safeItemsPerPage));
        }

        public static int ClampPage(int page, int itemCount, int itemsPerPage)
        {
            return Mathf.Clamp(page, 0, GetMaxPages(itemCount, itemsPerPage) - 1);
        }

        public static string FormatIndicator(int page, int itemCount, int itemsPerPage)
        {
            return string.Format("{0}/{1}", ClampPage(page, itemCount, itemsPerPage) + 1, GetMaxPages(itemCount, itemsPerPage));
        }
    }
}
