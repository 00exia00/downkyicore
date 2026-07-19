using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DownKyi.Commands;
using DownKyi.Core.BiliApi.Dynamic;
using DownKyi.Core.BiliApi.Dynamic.Models;
using DownKyi.Events;
using DownKyi.Images;
using DownKyi.Utils;
using DownKyi.ViewModels.PageViewModels;
using Prism.Commands;
using Prism.Events;
using Prism.Navigation.Regions;

namespace DownKyi.ViewModels;

internal sealed class ViewMyDynamicViewModel : ViewModelBase
{
    public const string Tag = "PageMyDynamic";

    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly HashSet<string> _loadedIds = new(StringComparer.Ordinal);
    private readonly DynamicLabels _labels;
    private CancellationTokenSource? _loadCancellation;
    private string _offset = string.Empty;
    private bool _hasMore = true;

    public ViewMyDynamicViewModel(IEventAggregator eventAggregator) : base(eventAggregator)
    {
        ArrowBack = NavigationIcon.Instance().ArrowBack;
        ArrowBack.Fill = DictionaryResource.GetColor("ColorTextDark");
        _labels = new DynamicLabels(
            DictionaryResource.GetString("DynamicForward"),
            DictionaryResource.GetString("DynamicComment"),
            DictionaryResource.GetString("DynamicLike"),
            DictionaryResource.GetString("DynamicForwardedFrom"));
    }

    private VectorImage _arrowBack = null!;

    public VectorImage ArrowBack
    {
        get => _arrowBack;
        private set => SetProperty(ref _arrowBack, value);
    }

    private RangeObservableCollection<DynamicCard> _items = new();

    public RangeObservableCollection<DynamicCard> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    private bool _contentVisibility;

    public bool ContentVisibility
    {
        get => _contentVisibility;
        private set => SetProperty(ref _contentVisibility, value);
    }

    private bool _loadingVisibility;

    public bool LoadingVisibility
    {
        get => _loadingVisibility;
        private set => SetProperty(ref _loadingVisibility, value);
    }

    private bool _noDataVisibility;

    public bool NoDataVisibility
    {
        get => _noDataVisibility;
        private set => SetProperty(ref _noDataVisibility, value);
    }

    private DelegateCommand? _backSpaceCommand;

    public DelegateCommand BackSpaceCommand =>
        _backSpaceCommand ??= new DelegateCommand(ExecuteBackSpace);

    protected internal override void ExecuteBackSpace()
    {
        CancelAndDispose(ref _loadCancellation);
        InitView();
        EventAggregator.GetEvent<NavigationEvent>().Publish(new NavigationParam
        {
            ViewName = ParentView,
            ParentViewName = null,
            Parameter = null
        });
    }

    private DownKyiAsyncDelegateCommand? _refreshCommand;

    public DownKyiAsyncDelegateCommand RefreshCommand =>
        _refreshCommand ??= new DownKyiAsyncDelegateCommand(RefreshAsync);

    private DownKyiAsyncDelegateCommand? _loadMoreCommand;

    public DownKyiAsyncDelegateCommand LoadMoreCommand =>
        _loadMoreCommand ??= new DownKyiAsyncDelegateCommand(LoadMoreAsync);

    private async Task RefreshAsync()
    {
        var cancellationToken = ReplaceCancellationSource(ref _loadCancellation);
        _offset = string.Empty;
        _hasMore = true;
        _loadedIds.Clear();
        await LoadPageAsync(reset: true, waitForTurn: true, cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadMoreAsync()
    {
        if (!_hasMore || NoDataVisibility)
        {
            return;
        }

        var cancellationToken = _loadCancellation?.Token
                                ?? ReplaceCancellationSource(ref _loadCancellation);
        await LoadPageAsync(reset: false, waitForTurn: false, cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadPageAsync(
        bool reset,
        bool waitForTurn,
        CancellationToken cancellationToken)
    {
        var entered = false;
        try
        {
            if (waitForTurn)
            {
                await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(true);
                entered = true;
            }
            else
            {
                entered = _loadGate.Wait(0, cancellationToken);
                if (!entered)
                {
                    return;
                }
            }

            LoadingVisibility = reset && Items.Count == 0;
            NoDataVisibility = false;

            var data = await DynamicApi.GetFeedAsync(
                reset ? null : _offset,
                cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();

            var cards = data?.Items
                .Where(item => item.Visible)
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && _loadedIds.Add(item.Id))
                .Select(item => Convert(item, EventAggregator, _labels))
                .Where(card => card != null)
                .Cast<DynamicCard>()
                .ToList() ?? new List<DynamicCard>();

            if (reset)
            {
                Items.ReplaceRange(cards);
            }
            else
            {
                Items.AddRange(cards);
            }

            _offset = data?.Offset ?? string.Empty;
            _hasMore = data is { HasMore: true } && !string.IsNullOrWhiteSpace(_offset);
            ContentVisibility = Items.Count > 0;
            NoDataVisibility = Items.Count == 0;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            LoadingVisibility = false;
            if (entered)
            {
                _loadGate.Release();
            }
        }
    }

    internal static DynamicCard? Convert(DynamicFeedItem item, IEventAggregator eventAggregator)
    {
        return Convert(
            item,
            eventAggregator,
            new DynamicLabels("转发", "评论", "点赞", "转发自"));
    }

    private static DynamicCard? Convert(
        DynamicFeedItem item,
        IEventAggregator eventAggregator,
        DynamicLabels labels)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(eventAggregator);

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            return null;
        }

        var majorOwner = item.Modules.Content.Major != null ? item : item.Original ?? item;
        var major = majorOwner.Modules.Content.Major;
        var description = BuildDescription(item, labels.ForwardedFrom);
        var pictures = GetPictures(major)
            .Select(picture => new DynamicPictureView { Source = NormalizeUrl(picture.Source) })
            .Where(picture => !string.IsNullOrWhiteSpace(picture.Source))
            .Take(9)
            .ToList();

        var cardTitle = GetCardTitle(major);
        var cardDescription = GetCardDescription(major);
        var cover = NormalizeUrl(GetCover(major));
        var bvid = major?.Archive?.Bvid ?? string.Empty;
        var contentUrl = NormalizeContentUrl(GetContentUrl(major), item.Id);
        var author = item.Modules.Author;

        return new DynamicCard(eventAggregator)
        {
            Id = item.Id,
            AuthorMid = author.Mid,
            AuthorName = author.Name,
            AuthorFace = NormalizeUrl(author.Face),
            PublishText = BuildPublishText(author),
            Description = description,
            CardTitle = cardTitle,
            CardDescription = cardDescription,
            Cover = cover,
            Bvid = bvid,
            ContentUrl = contentUrl,
            ForwardText = $"{labels.Forward} {item.Modules.Stats.Forward.Count}",
            CommentText = $"{labels.Comment} {item.Modules.Stats.Comment.Count}",
            LikeText = $"{labels.Like} {item.Modules.Stats.Like.Count}",
            HasDescription = !string.IsNullOrWhiteSpace(description),
            HasCard = !string.IsNullOrWhiteSpace(cardTitle) || !string.IsNullOrWhiteSpace(cover),
            HasCover = !string.IsNullOrWhiteSpace(cover),
            HasPictures = pictures.Count > 0,
            Pictures = pictures
        };
    }

    private static string BuildDescription(DynamicFeedItem item, string forwardedFromLabel)
    {
        var text = item.Modules.Content.Description?.Text
                   ?? item.Modules.Content.Major?.Opus?.Summary?.Text
                   ?? string.Empty;
        if (item.Original == null)
        {
            return text;
        }

        var originalText = item.Original.Modules.Content.Description?.Text
                           ?? item.Original.Modules.Content.Major?.Opus?.Summary?.Text
                           ?? string.Empty;
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return text;
        }

        var originalAuthor = item.Original.Modules.Author.Name;
        var prefix = string.IsNullOrWhiteSpace(originalAuthor)
            ? forwardedFromLabel
            : $"{forwardedFromLabel} @{originalAuthor}";
        return string.IsNullOrWhiteSpace(text)
            ? $"{prefix}：{originalText}"
            : $"{text}{Environment.NewLine}{prefix}：{originalText}";
    }

    private static string BuildPublishText(DynamicAuthor author)
    {
        var publishTime = author.PublishTime;
        if (string.IsNullOrWhiteSpace(publishTime) && author.PublishTimestamp > 0)
        {
            publishTime = DateTimeOffset
                .FromUnixTimeSeconds(author.PublishTimestamp)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        }

        return string.IsNullOrWhiteSpace(author.PublishAction)
            ? publishTime
            : $"{publishTime} · {author.PublishAction}".TrimStart(' ', '·');
    }

    private static IReadOnlyList<DynamicPicture> GetPictures(DynamicMajor? major)
    {
        return major?.Draw?.Items
               ?? major?.Opus?.Pictures
               ?? Array.Empty<DynamicPicture>();
    }

    private static string GetCardTitle(DynamicMajor? major)
    {
        return major?.Archive?.Title
               ?? major?.Article?.Title
               ?? major?.Opus?.Title
               ?? major?.Pgc?.Title
               ?? major?.Common?.Title
               ?? major?.Live?.Title
               ?? major?.Unavailable?.Tips
               ?? string.Empty;
    }

    private static string GetCardDescription(DynamicMajor? major)
    {
        return major?.Archive?.Description
               ?? major?.Article?.Description
               ?? major?.Pgc?.Description
               ?? major?.Common?.Description
               ?? major?.Live?.Description
               ?? string.Empty;
    }

    private static string GetCover(DynamicMajor? major)
    {
        var articleCovers = major?.Article?.Covers;
        return major?.Archive?.Cover
               ?? (articleCovers is { Count: > 0 } ? articleCovers[0] : null)
               ?? major?.Pgc?.Cover
               ?? major?.Common?.Cover
               ?? major?.Live?.Cover
               ?? string.Empty;
    }

    private static string GetContentUrl(DynamicMajor? major)
    {
        return major?.Archive?.JumpAddress
               ?? major?.Article?.JumpAddress
               ?? major?.Opus?.JumpAddress
               ?? major?.Pgc?.JumpAddress
               ?? major?.Common?.JumpAddress
               ?? major?.Live?.JumpAddress
               ?? string.Empty;
    }

    private static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return url.StartsWith("//", StringComparison.Ordinal) ? $"https:{url}" : url;
    }

    private static string NormalizeContentUrl(string? url, string dynamicId)
    {
        var normalized = NormalizeUrl(url);
        return string.IsNullOrWhiteSpace(normalized)
            ? $"https://t.bilibili.com/{dynamicId}"
            : normalized;
    }

    private void InitView()
    {
        Items.Clear();
        _loadedIds.Clear();
        _offset = string.Empty;
        _hasMore = true;
        ContentVisibility = false;
        LoadingVisibility = false;
        NoDataVisibility = false;
    }

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        ArgumentNullException.ThrowIfNull(navigationContext);
        base.OnNavigatedTo(navigationContext);
        ArrowBack.Fill = DictionaryResource.GetColor("ColorTextDark");

        var mid = navigationContext.Parameters.GetValue<long>("Parameter");
        if (mid == 0)
        {
            ReplaceCancellationSource(ref _loadCancellation);
            return;
        }

        InitView();
        RunFireAndForget(RefreshAsync(), nameof(RefreshAsync));
    }

    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        CancelAndDispose(ref _loadCancellation);
        base.OnNavigatedFrom(navigationContext);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            CancelAndDispose(ref _loadCancellation);
            _loadGate.Dispose();
        }

        base.Dispose(disposing);
    }

    private readonly record struct DynamicLabels(
        string Forward,
        string Comment,
        string Like,
        string ForwardedFrom);
}
