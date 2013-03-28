﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Model;
using NzbDrone.Core.Providers;

using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;
using NzbDrone.Test.Common.AutoMoq;

namespace NzbDrone.Core.Test.ProviderTests.DiskScanProviderTests
{
    
    public class CleanUpDropFolderFixture : CoreTest
    {
        [Test]
        public void should_do_nothing_if_no_files_are_found()
        {
            
            var folder = @"C:\Test\DropDir\The Office";

            Mocker.GetMock<DiskProvider>().Setup(s => s.GetFiles(folder, SearchOption.AllDirectories))
                    .Returns(new string[0]);

            
            Mocker.Resolve<DiskScanProvider>().CleanUpDropFolder(folder);

            
            Mocker.GetMock<IMediaFileService>().Verify(v => v.GetFileByPath(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_do_nothing_if_no_conflicting_files_are_found()
        {
            
            var folder = @"C:\Test\DropDir\The Office";
            var filename = Path.Combine(folder, "NotAProblem.avi");

            var episodeFile = Builder<EpisodeFile>.CreateNew()
                    .With(f => f.Path = filename.NormalizePath())
                    .With(f => f.SeriesId = 12345)
                    .Build();

            Mocker.GetMock<DiskProvider>().Setup(s => s.GetFiles(folder, SearchOption.AllDirectories))
                    .Returns(new string[] { filename });

            Mocker.GetMock<IMediaFileService>().Setup(s => s.GetFileByPath(filename))
                    .Returns(() => null);

            
            Mocker.Resolve<DiskScanProvider>().CleanUpDropFolder(folder);

            
            Mocker.GetMock<IMediaFileService>().Verify(v => v.GetFileByPath(filename), Times.Once());
            Mocker.GetMock<ISeriesRepository>().Verify(v => v.Get(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_move_file_if_a_conflict_is_found()
        {
            
            var folder = @"C:\Test\DropDir\The Office";
            var filename = Path.Combine(folder, "Problem.avi");
            var seriesId = 12345;
            var newFilename = "S01E01 - Title";
            var newFilePath = @"C:\Test\TV\The Office\Season 01\S01E01 - Title.avi";

            var episodeFile = Builder<EpisodeFile>.CreateNew()
                   .With(f => f.Path = filename.NormalizePath())
                   .With(f => f.SeriesId = seriesId)
                   .Build();

            var series = Builder<Series>.CreateNew()
                    .With(s => s.Id = seriesId)
                    .With(s => s.Title = "The Office")
                    .Build();

            var episode = Builder<Episode>.CreateListOfSize(1)
                .All()
                    .With(e => e.SeriesId = seriesId)
                    .With(e => e.EpisodeFile = episodeFile)
                    .Build().ToList();

            Mocker.GetMock<IMediaFileService>().Setup(v => v.GetFileByPath(filename))
                   .Returns(() => null);

            Mocker.GetMock<DiskProvider>().Setup(s => s.GetFiles(folder, SearchOption.AllDirectories))
                    .Returns(new string[] { filename });

            Mocker.GetMock<IMediaFileService>().Setup(s => s.GetFileByPath(filename))
                    .Returns(episodeFile);

            Mocker.GetMock<ISeriesRepository>().Setup(s => s.Get(It.IsAny<int>()))
                .Returns(series);

            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodesByFileId(episodeFile.Id))
                    .Returns(episode);

            Mocker.GetMock<IBuildFileNames>().Setup(s => s.BuildFilename(It.IsAny<IList<Episode>>(), series, It.IsAny<EpisodeFile>()))
                .Returns(newFilename);

            Mocker.GetMock<IBuildFileNames>().Setup(s => s.BuildFilePath(It.IsAny<Series>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(newFilePath);

            Mocker.GetMock<DiskProvider>()
                  .Setup(s => s.FileExists(filename))
                  .Returns(true);

            Mocker.GetMock<DiskProvider>().Setup(s => s.MoveFile(episodeFile.Path, newFilePath));

            
            Mocker.Resolve<DiskScanProvider>().CleanUpDropFolder(folder);

            
            Mocker.GetMock<IMediaFileService>().Verify(v => v.GetFileByPath(filename), Times.Once());
            Mocker.GetMock<DiskProvider>().Verify(v => v.MoveFile(filename.NormalizePath(), newFilePath), Times.Once());
        }
    }
}
