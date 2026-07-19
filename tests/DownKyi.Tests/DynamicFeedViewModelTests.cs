using DownKyi.Core.BiliApi.Dynamic.Models;
using DownKyi.ViewModels;
using Prism.Events;

namespace DownKyi.Tests;

#pragma warning disable CA1515 // xUnit requires public test classes.
public sealed class DynamicFeedViewModelTests
{
    [Fact]
    public void PersonalSpaceDynamicEntryMapsToDynamicFeedPage()
    {
        Assert.Equal(ViewMyDynamicViewModel.Tag, ViewMySpaceViewModel.ResolvePackageViewName(4));
    }

    [Fact]
    public void ArchiveDynamicConvertsToNavigableVideoCard()
    {
        var item = new DynamicFeedItem
        {
            Id = "1001",
            Modules = new DynamicModules
            {
                Author = new DynamicAuthor
                {
                    Mid = 42,
                    Name = "Uploader",
                    Face = "//example.test/avatar.jpg",
                    PublishTime = "1分钟前",
                    PublishAction = "投稿了视频"
                },
                Content = new DynamicContent
                {
                    Description = new DynamicText { Text = "dynamic text" },
                    Major = new DynamicMajor
                    {
                        Archive = new DynamicArchive
                        {
                            Bvid = "BV1test",
                            Cover = "//example.test/cover.jpg",
                            Title = "Video title",
                            Description = "Video description"
                        }
                    }
                },
                Stats = new DynamicStats
                {
                    Like = new DynamicStat { Count = 12 }
                }
            }
        };

        var card = ViewMyDynamicViewModel.Convert(item, new EventAggregator());

        Assert.NotNull(card);
        Assert.Equal("BV1test", card.Bvid);
        Assert.Equal("Video title", card.CardTitle);
        Assert.Equal("https://example.test/cover.jpg", card.Cover);
        Assert.Equal("https://example.test/avatar.jpg", card.AuthorFace);
        Assert.Equal("点赞 12", card.LikeText);
        Assert.True(card.HasCard);
    }

    [Fact]
    public void ForwardDynamicUsesOriginalPicturesAndText()
    {
        var item = new DynamicFeedItem
        {
            Id = "1002",
            Modules = new DynamicModules
            {
                Author = new DynamicAuthor { Name = "Forwarder" },
                Content = new DynamicContent
                {
                    Description = new DynamicText { Text = "forward comment" }
                }
            },
            Original = new DynamicFeedItem
            {
                Modules = new DynamicModules
                {
                    Author = new DynamicAuthor { Name = "OriginalUploader" },
                    Content = new DynamicContent
                    {
                        Description = new DynamicText { Text = "original text" },
                        Major = new DynamicMajor
                        {
                            Draw = new DynamicDraw
                            {
                                Items =
                                [
                                    new DynamicPicture { Source = "//example.test/image.jpg" }
                                ]
                            }
                        }
                    }
                }
            }
        };

        var card = ViewMyDynamicViewModel.Convert(item, new EventAggregator());

        Assert.NotNull(card);
        Assert.Contains("forward comment", card.Description, StringComparison.Ordinal);
        Assert.Contains("转发自 @OriginalUploader：original text", card.Description, StringComparison.Ordinal);
        Assert.True(card.HasPictures);
        Assert.Equal("https://example.test/image.jpg", card.Pictures[0].Source);
    }
}
#pragma warning restore CA1515
