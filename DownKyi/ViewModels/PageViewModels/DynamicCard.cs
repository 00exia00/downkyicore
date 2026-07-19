using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DownKyi.Commands;
using DownKyi.Core.BiliApi.BiliUtils;
using DownKyi.Utils;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace DownKyi.ViewModels.PageViewModels;

internal sealed class DynamicCard : BindableBase
{
    private readonly IEventAggregator _eventAggregator;

    public DynamicCard(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }

    public string Id { get; set; } = string.Empty;
    public long AuthorMid { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorFace { get; set; } = string.Empty;
    public string PublishText { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CardTitle { get; set; } = string.Empty;
    public string CardDescription { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string Bvid { get; set; } = string.Empty;
    public string ContentUrl { get; set; } = string.Empty;
    public string ForwardText { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public string LikeText { get; set; } = string.Empty;
    public bool HasDescription { get; set; }
    public bool HasCard { get; set; }
    public bool HasCover { get; set; }
    public bool HasPictures { get; set; }
    public IReadOnlyList<DynamicPictureView> Pictures { get; set; } = Array.Empty<DynamicPictureView>();

    private DelegateCommand? _authorCommand;

    public DelegateCommand AuthorCommand =>
        _authorCommand ??= new DelegateCommand(ExecuteAuthorCommand, () => AuthorMid > 0);

    private void ExecuteAuthorCommand()
    {
        NavigateToView.NavigateToViewUserSpace(
            _eventAggregator,
            ViewMyDynamicViewModel.Tag,
            AuthorMid);
    }

    private DownKyiAsyncDelegateCommand? _openContentCommand;

    public DownKyiAsyncDelegateCommand OpenContentCommand =>
        _openContentCommand ??= new DownKyiAsyncDelegateCommand(
            OpenContentAsync,
            () => !string.IsNullOrWhiteSpace(Bvid) || !string.IsNullOrWhiteSpace(ContentUrl));

    private Task OpenContentAsync()
    {
        if (!string.IsNullOrWhiteSpace(Bvid))
        {
            NavigateToView.NavigationView(
                _eventAggregator,
                ViewVideoDetailViewModel.Tag,
                ViewMyDynamicViewModel.Tag,
                $"{ParseEntrance.VideoUrl}{Bvid}");
            return Task.CompletedTask;
        }

        return PlatformHelper.OpenUrl(ContentUrl, _eventAggregator);
    }
}

internal sealed class DynamicPictureView
{
    public string Source { get; init; } = string.Empty;
}
