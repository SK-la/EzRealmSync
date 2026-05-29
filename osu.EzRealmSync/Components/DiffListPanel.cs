using osu.EzRealmSync.UI;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.EzRealmSync.Models;
using osuTK;
using osuTK.Input;
using RectangleF = osu.Framework.Graphics.Primitives.RectangleF;

namespace osu.EzRealmSync.Components
{
    /// <summary>
    /// 支持整行点击、Ctrl/Shift 多选、框选的 Diff 列表。
    /// </summary>
    public partial class DiffListPanel : CompositeDrawable
    {
        private readonly List<DiffItem> items;

        private BasicScrollContainer scroll = null!;
        private FillFlowContainer<DiffListRow> flow = null!;
        private Box selectionBox = null!;

        private bool isBoxSelecting;
        private Vector2 boxStartLocal;
        private int anchorIndex = -1;
        private bool additiveBoxSelection;

        public event Action? SelectionChanged;

        public int ItemCount => items.Count;

        public DiffListPanel(IEnumerable<DiffItem> items)
        {
            this.items = items.ToList();
        }

        public IEnumerable<DiffItem> GetSelectedItems()
        {
            return flow.Children.Where(r => r.Selected.Value).Select(r => r.Item);
        }

        public void SelectAll(bool selected)
        {
            foreach (var row in flow.Children)
                row.Selected.Value = selected;

            SelectionChanged?.Invoke();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                scroll = new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = flow = new FillFlowContainer<DiffListRow>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = items.Select((item, index) => createRow(item, index)).ToArray(),
                    },
                },
                selectionBox = new Box
                {
                    Colour = EzTheme.Accent,
                    Size = Vector2.Zero,
                    Alpha = 0,
                },
            };
        }

        private DiffListRow createRow(DiffItem item, int index)
        {
            var row = new DiffListRow(item, index);
            row.Selected.BindValueChanged(_ => SelectionChanged?.Invoke());
            return row;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            var row = getRowAt(e.ScreenSpaceMousePosition);

            if (row != null)
            {
                handleRowClick(row, e.ShiftPressed, e.ControlPressed);
                return true;
            }

            if (!e.ControlPressed && !e.ShiftPressed)
                clearSelection();

            isBoxSelecting = true;
            additiveBoxSelection = e.ControlPressed;
            boxStartLocal = ToLocalSpace(e.ScreenSpaceMousePosition);
            selectionBox.Position = boxStartLocal;
            selectionBox.Size = Vector2.Zero;
            selectionBox.Alpha = 0.25f;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (!isBoxSelecting)
                return;

            var currentLocal = ToLocalSpace(e.ScreenSpaceMousePosition);
            var topLeft = Vector2.ComponentMin(boxStartLocal, currentLocal);
            var size = Vector2.ComponentMax(boxStartLocal, currentLocal) - topLeft;

            selectionBox.Position = topLeft;
            selectionBox.Size = size;

            applyBoxSelection(getScreenSpaceRect(topLeft, size), additiveBoxSelection);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            if (!isBoxSelecting)
                return;

            isBoxSelecting = false;
            selectionBox.Alpha = 0;
            SelectionChanged?.Invoke();
        }

        private void handleRowClick(DiffListRow row, bool shift, bool ctrl)
        {
            if (shift && anchorIndex >= 0)
            {
                int start = Math.Min(anchorIndex, row.Index);
                int end = Math.Max(anchorIndex, row.Index);

                if (!ctrl)
                    clearSelection();

                for (int i = start; i <= end; i++)
                    flow.Children[i].Selected.Value = true;
            }
            else if (ctrl)
            {
                row.Selected.Value = !row.Selected.Value;
                anchorIndex = row.Index;
            }
            else
            {
                bool onlyThisSelected = row.Selected.Value && flow.Children.Count(r => r.Selected.Value) == 1;

                clearSelection();

                if (!onlyThisSelected)
                    row.Selected.Value = true;

                anchorIndex = row.Index;
            }

            SelectionChanged?.Invoke();
        }

        private void applyBoxSelection(RectangleF screenRect, bool additive)
        {
            if (!additive)
                clearSelection();

            foreach (var row in flow.Children)
            {
                if (screenRect.IntersectsWith(row.ScreenSpaceDrawQuad.AABBFloat))
                    row.Selected.Value = true;
            }
        }

        private DiffListRow? getRowAt(Vector2 screenSpacePosition)
        {
            foreach (var row in flow.Children)
            {
                if (row.ScreenSpaceDrawQuad.AABBFloat.Contains(screenSpacePosition))
                    return row;
            }

            return null;
        }

        private void clearSelection()
        {
            foreach (var row in flow.Children)
                row.Selected.Value = false;
        }

        private RectangleF getScreenSpaceRect(Vector2 topLeftLocal, Vector2 size)
        {
            var topLeft = ToScreenSpace(topLeftLocal);
            var bottomRight = ToScreenSpace(topLeftLocal + size);
            return new RectangleF(
                topLeft.X,
                topLeft.Y,
                bottomRight.X - topLeft.X,
                bottomRight.Y - topLeft.Y);
        }
    }
}
