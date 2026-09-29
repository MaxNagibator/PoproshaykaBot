using KeepShell.Services.Modal;
using KeepShell.ViewModels;
using PoproshaykaBot.Core.Dashboard;
using PoproshaykaBot.Core.Settings.Ui;
using PoproshaykaBot.Wpf.Infrastructure.Dashboard;
using PoproshaykaBot.Wpf.ViewModels;

namespace PoproshaykaBot.Wpf.Tests.Dashboard;

[TestFixture]
public class DashboardEditSessionTests
{
    [Test]
    public void A_resize_that_keeps_the_projection_still_reaches_the_file()
    {
        var store = new FakeLayoutStore(SideBySide());
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        Assert.That(session.Resize([], [0.55, 0.45]), Is.True);

        session.Flush();

        var weights = Weights(store.Saved);

        Assert.Multiple(() =>
        {
            Assert.That(weights[0], Is.EqualTo(0.55).Within(0.001),
                "Сдвиг разделителя не меняет клеток проекции, и без владения деревом его затирало бы содержимым файла.");
            Assert.That(store.Saved!.Tiles.Select(tile => tile.Column), Is.EqualTo(new[] { 0, 2 }),
                "Клетки при этом остаются прежними – проекция округляет доли до сетки.");
        });
    }

    [Test]
    public void The_write_waits_for_the_debounce_and_happens_once()
    {
        var store = new FakeLayoutStore(SideBySide());
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        session.Resize([], [0.6, 0.4]);
        session.Resize([], [0.7, 0.3]);

        Assert.That(store.SaveCount, Is.Zero, "До истечения задержки правка в файл не уходит.");

        time.Advance(DashboardEditSession.WriteDelay);

        Assert.Multiple(() =>
        {
            Assert.That(store.SaveCount, Is.EqualTo(1), "Две правки подряд дают одну запись, а не две.");
            Assert.That(Weights(store.Saved)[0], Is.EqualTo(0.7).Within(0.001));
        });
    }

    [Test]
    public void Undo_returns_the_previous_layout_whole()
    {
        var store = new FakeLayoutStore(SideBySide());
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        session.Remove("twitch-chat");

        Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "twitch-chat").IsVisible, Is.False,
            "Удаление гасит запись, а не выбрасывает её – иначе набор типов перестаёт быть монотонным.");

        Assert.That(session.Undo(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "twitch-chat").IsVisible, Is.True);
            Assert.That(session.CanUndo, Is.False);
        });
    }

    [Test]
    public void A_write_from_another_editor_replaces_the_draft()
    {
        var store = new FakeLayoutStore(SideBySide());
        var time = new ManualTimeProvider();
        var coordinator = new DashboardLayoutCoordinator(store);

        using var session = new DashboardEditSession(coordinator, time);

        session.Resize([], [0.6, 0.4]);

        coordinator.Mutate(layout =>
        {
            layout!.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed = true;

            return layout;
        });

        Assert.Multiple(() =>
        {
            Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.True,
                "Чужая запись обязана заменить черновик, иначе следующий дебаунс воскресит устаревшую раскладку.");
            Assert.That(session.CanUndo, Is.False, "Стек отмены после чужой записи ведёт в снятое состояние – его чистят.");
        });

        time.Advance(DashboardEditSession.WriteDelay);

        Assert.That(store.SaveCount, Is.EqualTo(1), "Отложенная запись снята вместе с черновиком, лишней записи нет.");
    }

    [Test]
    public void A_collapse_during_the_edit_reaches_the_file_at_once_and_keeps_the_draft_with_its_undo()
    {
        var store = new FakeLayoutStore(SideBySide());
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        session.Resize([], [0.6, 0.4]);

        Assert.That(session.SetCollapsed("stream-info", true), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(store.Saved!.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.True,
                "Свёртку файл хранит сам, и коммит черновика берёт её с диска – поэтому она пишется сразу, а не дебаунсом.");
            Assert.That(Weights(session.Draft)[0], Is.EqualTo(0.6).Within(0.001), "Своя запись свёртки не заменяет черновик снимком с диска.");
            Assert.That(session.CanUndo, Is.True, "…и не чистит стек отмены.");
        });

        time.Advance(DashboardEditSession.WriteDelay);

        Assert.Multiple(() =>
        {
            Assert.That(Weights(store.Saved)[0], Is.EqualTo(0.6).Within(0.001), "Отложенный сдвиг доезжает до файла следом.");
            Assert.That(store.Saved!.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.True);
            Assert.That(session.CanUndo, Is.True, "Запись после своей же свёртки не считается слиянием с чужой правкой.");
        });

        session.Undo();

        Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.True,
            "Отмена возвращает дерево, а свёртку оставляет той, что лежит в файле.");
    }

    [Test]
    public void A_collapse_the_file_refused_is_taken_back_from_the_draft_and_its_undo()
    {
        var store = new FakeLayoutStore(SideBySide());
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        session.Resize([], [0.6, 0.4]);
        store.FailSaves = true;

        Assert.That(session.SetCollapsed("stream-info", true), Is.False,
            "Сорвавшаяся запись свёртки – отказ, иначе вью-модель оставит плитку свёрнутой поверх несвёрнутого файла.");
        Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.False,
            "Черновик возвращается к файлу: коммит всё равно взял бы свёртку с диска, но молча и без перерисовки.");

        store.FailSaves = false;
        session.Undo();

        Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.False,
            "Снимки отмены откатываются вместе с черновиком.");
    }

    [Test]
    public void Reset_to_defaults_keeps_the_collapse_the_file_holds()
    {
        var layout = SideBySide();

        layout.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed = true;

        var store = new FakeLayoutStore(layout);
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        session.ResetToDefaults();

        Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.True,
            "Свёртку держит файл, и сброс её не снимает: иначе правка показала бы плитку раскрытой, а коммит молча свернул бы её обратно.");

        time.Advance(DashboardEditSession.WriteDelay);

        Assert.That(store.Saved!.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.True);
    }

    [Test]
    public void A_late_event_of_an_older_revision_does_not_replace_a_newer_draft()
    {
        var store = new FakeLayoutStore(SideBySide());
        var coordinator = new DashboardLayoutCoordinator(store);
        var nested = false;

        coordinator.LayoutChanged += (_, _) =>
        {
            if (nested)
            {
                return;
            }

            nested = true;

            coordinator.Mutate(current =>
            {
                current!.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed = true;

                return current;
            });
        };

        using var session = new DashboardEditSession(coordinator, new ManualTimeProvider());

        coordinator.Mutate(current =>
        {
            current!.Tiles.Single(tile => tile.TypeId == "twitch-chat").IsCollapsed = true;

            return current;
        });

        Assert.Multiple(() =>
        {
            Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "stream-info").IsCollapsed, Is.True,
                "Событие вложенной записи доходит раньше внешней, и запоздавшее событие младшей ревизии не должно откатить черновик к более старому снимку.");
            Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "twitch-chat").IsCollapsed, Is.True);
        });
    }

    [Test]
    public void A_split_past_the_grid_ceiling_is_refused()
    {
        var layout = SideBySide();
        var session = default(DashboardEditSession);

        try
        {
            session = new(new(new FakeLayoutStore(layout)), new ManualTimeProvider());

            var added = new[] { "broadcast-status", "broadcast-profiles", "polls-control", "obs-info", "chat-overlay-preview", "stream-history" };
            var target = "twitch-chat";

            foreach (var typeId in added)
            {
                Assert.That(session.Add(typeId, target, PaneSide.Right), Is.EqualTo(DashboardEditStatus.Applied), $"Плитка {typeId} ещё помещается в восемь колонок.");
                target = typeId;
            }

            Assert.That(session.Add("chat-overlay-preview", target, PaneSide.Right), Is.EqualTo(DashboardEditStatus.Rejected),
                "Плитка, уже стоящая в дереве, вторым листом не становится, и упор тут не в сетку.");
            Assert.That(session.Add("audience-tracker", target, PaneSide.Right), Is.EqualTo(DashboardEditStatus.GridFull),
                "Девятая колонка пробила бы потолок сетки, а проекция при чтении зажимается до восьми.");
        }
        finally
        {
            session?.Dispose();
        }
    }

    [TestCase(false, false, Description = "Добавление в черновик без дерева разрезов")]
    [TestCase(true, false, Description = "Добавление в закрытую сессию")]
    [TestCase(false, true, Description = "Перенос в черновике без дерева разрезов")]
    [TestCase(true, true, Description = "Перенос в закрытой сессии")]
    public void An_edit_without_a_live_tree_is_not_reported_as_a_failed_split(bool disposeFirst, bool byMove)
    {
        var layout = SideBySide();

        if (!disposeFirst)
        {
            layout.Root = null;
        }

        var session = new DashboardEditSession(new(new FakeLayoutStore(layout)), new ManualTimeProvider());

        if (disposeFirst)
        {
            session.Dispose();
        }

        var status = byMove
            ? session.Move([0], [1], PaneSide.Bottom)
            : session.Add("audience-tracker", "twitch-chat", PaneSide.Right);

        if (!disposeFirst)
        {
            session.Dispose();
        }

        Assert.That(status, Is.EqualTo(DashboardEditStatus.Unavailable),
            "Разреза здесь не было вовсе, и валить отказ на раскладку – значит звать пользователя чинить то, что цело.");
    }

    [TestCase(false, Description = "Девятая строка от новой плитки")]
    [TestCase(true, Description = "Девятая строка от переноса соседней плитки")]
    public void A_ninth_row_is_refused_as_the_grid_ceiling(bool byMove)
    {
        var session = new DashboardEditSession(new(new FakeLayoutStore(SideBySide())), new ManualTimeProvider());

        try
        {
            var bottom = "stream-info";

            foreach (var typeId in new[] { "audience-tracker", "broadcast-status", "broadcast-profiles", "polls-control", "obs-info", "chat-overlay-preview", "stream-history" })
            {
                Assert.That(session.Add(typeId, bottom, PaneSide.Bottom), Is.EqualTo(DashboardEditStatus.Applied),
                    $"Плитка {typeId} ещё помещается в восемь строк.");

                bottom = typeId;
            }

            var status = byMove
                ? session.Move(PathOf(session, "twitch-chat"), PathOf(session, bottom), PaneSide.Bottom)
                : session.Add("logs", bottom, PaneSide.Bottom);

            Assert.That(status, Is.EqualTo(DashboardEditStatus.GridFull),
                "Девятая строка упирается в тот же потолок, что и девятая колонка, и на перетаскивании он назван так же.");
        }
        finally
        {
            session.Dispose();
        }
    }

    [TestCase(new[] { 0 }, Description = "Плитку переносят саму на себя")]
    [TestCase(new[] { 7 }, Description = "Пути цели в дереве нет")]
    public void A_move_that_the_tree_refuses_is_not_reported_as_a_full_grid(int[] target)
    {
        var store = new FakeLayoutStore(SideBySide());

        using var session = new DashboardEditSession(new(store), new ManualTimeProvider());

        Assert.That(session.Move([0], target, PaneSide.Bottom), Is.EqualTo(DashboardEditStatus.Rejected),
            "Сетка здесь свободна, и звать пользователя освобождать место было бы ложью.");
    }

    [Test]
    public void A_move_onto_a_hole_divides_it()
    {
        var store = new FakeLayoutStore(ColumnWithAHole());

        using var session = new DashboardEditSession(new(store), new ManualTimeProvider());

        Assert.That(session.Move([0], [1], PaneSide.Bottom), Is.EqualTo(DashboardEditStatus.Applied),
            "Бросок на пустую ячейку – тот же перенос, что и на соседнюю плитку.");

        session.Flush();

        Assert.That(store.Saved!.Tiles.Single().Row, Is.EqualTo(2),
            "Плитка ушла в нижнюю половину дыры, а верхняя осталась пустой.");
    }

    [Test]
    public void A_move_onto_the_spot_the_tile_already_holds_keeps_the_draft_as_it_was()
    {
        var layout = SideBySide();

        layout.Root = new SplitPane(SplitOrientation.Columns,
        [
            new(new TilePane("stream-info"), 0.2),
            new(new TilePane("twitch-chat"), 0.8),
        ]);

        var store = new FakeLayoutStore(layout);
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        Assert.That(session.Move([1], [0], PaneSide.Right), Is.EqualTo(DashboardEditStatus.Applied),
            "Бросок на место, где плитка уже стоит, законен – отказывать пользователю не в чем.");

        time.Advance(DashboardEditSession.WriteDelay);

        Assert.Multiple(() =>
        {
            Assert.That(Weights(session.Draft), Is.EqualTo(new double?[] { 0.2, 0.8 }));
            Assert.That(session.CanUndo, Is.False, "Шаг отмены здесь съел бы предыдущую настоящую правку.");
            Assert.That(store.SaveCount, Is.Zero, "Менять нечего – и отложенной записи заводить не за чем.");
        });
    }

    [Test]
    public void Removing_the_last_leaf_is_refused()
    {
        var layout = SideBySide();

        layout.Root = new TilePane("stream-info");
        layout.Tiles.Single(tile => tile.TypeId == "twitch-chat").IsVisible = false;

        var store = new FakeLayoutStore(layout);

        using var session = new DashboardEditSession(new(store), new ManualTimeProvider());

        Assert.Multiple(() =>
        {
            Assert.That(session.Remove("stream-info"), Is.EqualTo(DashboardRemoveStatus.LastTile),
                "Пустая панель закрывает сам режим правки, и вернуть плитку оттуда уже нечем – последний лист не удаляется.");
            Assert.That(session.Draft.Tiles.Single(tile => tile.TypeId == "stream-info").IsVisible, Is.True);
        });
    }

    [Test]
    public void A_refused_operation_leaves_the_draft_untouched()
    {
        var store = new FakeLayoutStore(SideBySide());
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        var before = session.Draft.Tiles.Count;

        Assert.That(session.Add("audience-tracker", "twitch-chat", PaneSide.Right), Is.EqualTo(DashboardEditStatus.Applied));

        var target = "audience-tracker";

        foreach (var typeId in new[] { "broadcast-status", "broadcast-profiles", "polls-control", "obs-info", "chat-overlay-preview" })
        {
            Assert.That(session.Add(typeId, target, PaneSide.Right), Is.EqualTo(DashboardEditStatus.Applied));
            target = typeId;
        }

        var placed = session.Draft.Tiles.Count;

        Assert.That(session.Add("stream-history", target, PaneSide.Right), Is.EqualTo(DashboardEditStatus.GridFull), "Девятая колонка пробила бы потолок сетки.");

        Assert.Multiple(() =>
        {
            Assert.That(session.Draft.Tiles, Has.Count.EqualTo(placed),
                "Отказ обязан откатить и запись, которую операция завела для новой плитки, иначе черновик остаётся наполовину применённым.");
            Assert.That(placed, Is.GreaterThan(before));
        });
    }

    [Test]
    public void The_edit_entry_is_offered_for_a_tree_layout()
    {
        using var dashboard = new DashboardViewModel(
            [new FakeTile("stream-info"), new FakeTile("twitch-chat", fills: true)],
            new(new FakeLayoutStore(SideBySide())),
            TimeProvider.System);

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.Pane, Is.Not.Null);
            Assert.That(dashboard.CanEdit, Is.True, "Дерево есть и стопки нет – кнопка входа в режим правки обязана быть доступна.");
            Assert.That(dashboard.IsEditing, Is.False);
        });

        dashboard.ToggleEditCommand.Execute(null);

        Assert.That(dashboard.IsEditing, Is.True);

        dashboard.SetStacked(true);

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.IsEditing, Is.False, "Стопка закрывает режим: долей там нет.");
            Assert.That(dashboard.CanEdit, Is.False);
        });
    }

    [Test]
    public void Entering_the_edit_mode_marks_every_tile_and_leaving_it_clears_the_mark()
    {
        var streamInfo = new FakeTile("stream-info");
        var chat = new FakeTile("twitch-chat", fills: true);

        using var dashboard = new DashboardViewModel(
            [streamInfo, chat],
            new(new FakeLayoutStore(SideBySide())),
            TimeProvider.System);

        dashboard.ToggleEditCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(chat.IsLayoutEditing, Is.True, "Плитка чата обязана узнать о режиме правки: без этого WebView2 закрывает собой подсказку броска.");
            Assert.That(streamInfo.IsLayoutEditing, Is.True);
        });

        dashboard.ToggleEditCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(chat.IsLayoutEditing, Is.False);
            Assert.That(streamInfo.IsLayoutEditing, Is.False);
        });
    }

    [Test]
    public void Disposing_the_dashboard_in_the_edit_mode_clears_the_mark()
    {
        var streamInfo = new FakeTile("stream-info");
        var chat = new FakeTile("twitch-chat", fills: true);

        var dashboard = new DashboardViewModel(
            [streamInfo, chat],
            new(new FakeLayoutStore(SideBySide())),
            TimeProvider.System);

        dashboard.ToggleEditCommand.Execute(null);
        dashboard.Dispose();

        Assert.That(chat.IsLayoutEditing, Is.False,
            "Плитки переживают страницу «Обзор»: метка, оставшаяся после закрытия, держала бы чат скрытым до перезапуска.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void A_modal_dialog_marks_every_tile_until_it_closes(bool closesItself)
    {
        var streamInfo = new FakeTile("stream-info");
        var chat = new FakeTile("twitch-chat", fills: true);
        var modal = new ModalHostViewModel();
        var dialog = new FakeDialog();

        using var dashboard = new DashboardViewModel(
            [streamInfo, chat],
            new(new FakeLayoutStore(SideBySide())),
            TimeProvider.System,
            modal: modal);

        _ = modal.ShowAsync(dialog);

        Assert.Multiple(() =>
        {
            Assert.That(chat.IsModalDialogOpen, Is.True, "WebView2 – своё окно поверх WPF и закрыл бы собой диалог.");
            Assert.That(streamInfo.IsModalDialogOpen, Is.True);
        });

        dashboard.ToggleEditCommand.Execute(null);

        if (closesItself)
        {
            dialog.Close(true);
        }
        else
        {
            modal.RequestCancelCommand.Execute(null);
        }

        Assert.Multiple(() =>
        {
            Assert.That(chat.IsModalDialogOpen, Is.False, "Закрытый диалог обязан вернуть браузер.");
            Assert.That(chat.IsLayoutEditing, Is.True, "Закрытие диалога не выводит из режима правки: признаки независимы.");
        });

        _ = modal.ShowAsync(dialog);
        dashboard.ToggleEditCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(chat.IsLayoutEditing, Is.False);
            Assert.That(chat.IsModalDialogOpen, Is.True, "Выход из режима правки под открытым диалогом не должен показать браузер поверх диалога.");
        });

        modal.RequestCancelCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(chat.IsModalDialogOpen, Is.False);
            Assert.That(chat.IsLayoutEditing, Is.False);
        });
    }

    [Test]
    public void A_dialog_open_before_the_dashboard_marks_the_tiles_and_disposing_clears_the_mark()
    {
        var chat = new FakeTile("twitch-chat", fills: true);
        var modal = new ModalHostViewModel();

        _ = modal.ShowAsync(new FakeDialog());

        var dashboard = new DashboardViewModel(
            [new FakeTile("stream-info"), chat],
            new(new FakeLayoutStore(SideBySide())),
            TimeProvider.System,
            modal: modal);

        Assert.That(chat.IsModalDialogOpen, Is.True, "Диалог, открытый до создания «Обзора», тоже закрыл бы собой браузер.");

        dashboard.Dispose();

        Assert.That(chat.IsModalDialogOpen, Is.False, "Плитки переживают страницу «Обзор»: метка после закрытия держала бы чат скрытым.");

        modal.RequestCancelCommand.Execute(null);
        _ = modal.ShowAsync(new FakeDialog());

        Assert.That(chat.IsModalDialogOpen, Is.False, "Освобождённая панель отписана от хоста диалогов.");
    }

    private sealed class FakeDialog : IDialogViewModel
    {
        public event EventHandler<bool>? RequestClose;

        public void Close(bool result)
        {
            RequestClose?.Invoke(this, result);
        }
    }

    [Test]
    public void A_resize_next_to_a_hole_reaches_the_file_and_keeps_the_hole()
    {
        var store = new FakeLayoutStore(ColumnWithAHole());
        var time = new ManualTimeProvider();

        using var session = new DashboardEditSession(new(store), time);

        Assert.That(session.Resize([], [0.75, 0.25]), Is.True, "Дыра – законный лист, и узел с ней поддаётся разделителю.");

        session.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(Weights(store.Saved)[0], Is.EqualTo(0.75).Within(0.001));
            Assert.That(store.Saved!.Tiles.Where(tile => tile.IsVisible).Select(tile => tile.TypeId),
                Is.EqualTo(new[] { "stream-info" }), "Пустышка не заводит себе записи в Tiles.");
            Assert.That(store.Saved.Tiles.Single().RowSpan, Is.EqualTo(3), "Доля 0.75 от четырёх строк отдаёт плитке три, дыре – одну.");
        });
    }

    private static IReadOnlyList<int> PathOf(DashboardEditSession session, string typeId)
    {
        Assert.That(DashboardPaneEditor.TryFindPath(session.Draft.Root!, typeId, out var path), Is.True,
            $"Плитка {typeId} обязана быть в дереве черновика.");

        return path;
    }

    private static DashboardLayoutSettings ColumnWithAHole()
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 1,
            RowCount = 4,
            Root = new SplitPane(SplitOrientation.Rows,
            [
                new(new TilePane("stream-info"), 0.5),
                new(new TilePane(DashboardLayoutTree.EmptySlotTypeId), 0.5),
            ]),
        };

        layout.Tiles.Add(new() { Id = "stream-info", TypeId = "stream-info", Order = 0, Row = 0, Column = 0, ColumnSpan = 1, RowSpan = 2, IsVisible = true });

        return layout;
    }

    private static DashboardLayoutSettings SideBySide()
    {
        var layout = new DashboardLayoutSettings
        {
            ColumnCount = 4,
            RowCount = 1,
            Root = new SplitPane(SplitOrientation.Columns,
            [
                new(new TilePane("stream-info"), 0.5),
                new(new TilePane("twitch-chat"), 0.5),
            ]),
        };

        layout.Tiles.Add(new() { Id = "stream-info", TypeId = "stream-info", Order = 0, Row = 0, Column = 0, ColumnSpan = 2, RowSpan = 1, IsVisible = true });
        layout.Tiles.Add(new() { Id = "twitch-chat", TypeId = "twitch-chat", Order = 1, Row = 0, Column = 2, ColumnSpan = 2, RowSpan = 1, IsVisible = true });

        return layout;
    }

    private static double?[] Weights(DashboardLayoutSettings? layout)
    {
        return layout?.Root is SplitPane split
            ? split.Children.Select(slot => slot.Weight).ToArray()
            : [];
    }
}
