
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData.Operators;

namespace DynamicData.Binding
{
    /// <summary>
    ///     Container for paged bindings
    /// </summary>
    public class PageParameterData : NotifyPropertyChangedBase
    {
        private readonly ObservableCollection<int> _pages = new ObservableCollection<int>();

        private int _currentPage;
        private int _pageCount;
        private int _pageSize;
        private int _selectedPage;
        private int _totalCount;


        /// <summary>
        /// Gets the pages.
        /// </summary>
        /// <value>
        /// The pages.
        /// </value>
        public ObservableCollection<int> Pages
        {
            get { return _pages; }
        }


        /// <summary>
        /// Gets or sets the selected page.
        /// </summary>
        /// <value>
        /// The selected page.
        /// </value>
        public int SelectedPage
        {
            get { return _selectedPage; }
            set
            {
                if (_selectedPage == value) return;

                _selectedPage = value;
                OnPropertyChanged(() => SelectedPage);
            }
        }

        /// <summary>
        /// Gets the total count.
        /// </summary>
        /// <value>
        /// The total count.
        /// </value>
        public int TotalCount
        {
            get { return _totalCount; }
            private set
            {
                if (_totalCount != value)
                {
                    _totalCount = value;
                    OnPropertyChanged(() => TotalCount);
                }
            }
        }

        /// <summary>
        /// Gets the page count.
        /// </summary>
        /// <value>
        /// The page count.
        /// </value>
        public int PageCount
        {
            get { return _pageCount; }
            private set
            {
                if (_pageCount != value)
                {
                    _pageCount = value;
                    OnPropertyChanged(() => PageCount);
                }
            }
        }

        /// <summary>
        /// Gets the current page.
        /// </summary>
        /// <value>
        /// The current page.
        /// </value>
        public int CurrentPage
        {
            get { return _currentPage; }
            private set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged(() => CurrentPage);
                }
            }
        }

        /// <summary>
        /// Gets or sets the size of the page.
        /// </summary>
        /// <value>
        /// The size of the page.
        /// </value>
        public int PageSize
        {
            get { return _pageSize; }
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    OnPropertyChanged(() => PageSize);
                }
            }
        }

        /// <summary>
        /// Updates the values to reflect the page response
        /// </summary>
        /// <param name="response">The response.</param>
        public void Update(IPageResponse response)
        {
            CurrentPage = response.Page;
            PageSize = response.PageSize;
            PageCount = response.Pages;
            TotalCount = response.TotalSize;
            BuildPages();
        }

        private void BuildPages()
        {
            //clear excessive pages
            if (Pages.Count > PageCount)
            {
                var toClear = Pages.Where(p => p > PageCount).ToList();
                foreach (int i in toClear)
                {
                    Pages.Remove(i);
                }
            }

            else if (Pages.Count < PageCount)
            {
                var toadd = Enumerable.Range(Pages.Count + 1, PageCount - Pages.Count).ToList();
                foreach (int i in toadd)
                {
                    Pages.Add(i);
                }
            }
        }
    }
}