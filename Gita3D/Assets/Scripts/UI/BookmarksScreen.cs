using Gita.App;

namespace Gita.UI
{
    /// <summary>The verses the reader has marked.</summary>
    public sealed class BookmarksScreen : VerseListScreen
    {
        protected override string Title => "Saved verses";

        public override void OnShow()
        {
            ReadingLog.Changed += Refresh;
            Refresh();
        }

        public override void OnHidden() => ReadingLog.Changed -= Refresh;

        void Refresh()
        {
            Show(ReadingLog.Bookmarks(),
                "No saved verses yet.\n\nTap the bookmark on any verse\nto keep it here.",
                countSuffix: ReadingLog.BookmarkCount == 1 ? "SAVED" : "SAVED");
        }
    }
}
