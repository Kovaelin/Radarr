﻿

using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Jobs.Implementations;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Model.Notification;
using NzbDrone.Core.Providers;

using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;
using NzbDrone.Test.Common.AutoMoq;

namespace NzbDrone.Core.Test.JobTests
{ 
    [TestFixture]
    
    public class ImportNewSeriesJobTest : CoreTest
    {
        [Test]
        public void import_new_series_succesful()
        {
            var series = Builder<Series>.CreateListOfSize(2)
                     .All().With(s => s.LastInfoSync = null)
                     .TheFirst(1).With(s => s.Id = 12)
                     .TheNext(1).With(s => s.Id = 15)
                        .Build();

            var notification = new ProgressNotification("Test");

            WithStrictMocker();

            Mocker.GetMock<ISeriesRepository>()
                .Setup(p => p.All())
                .Returns(series);


            Mocker.GetMock<DiskScanJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)))
                .Callback(() => series[0].LastDiskSync = DateTime.Now);

            Mocker.GetMock<DiskScanJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[1].Id)))
                .Callback(() => series[1].LastDiskSync = DateTime.Now);

            Mocker.GetMock<XemUpdateJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") > 0)));

            Mocker.GetMock<UpdateInfoJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)))
                .Callback(() => series[0].LastInfoSync = DateTime.Now);

            Mocker.GetMock<UpdateInfoJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[1].Id)))
                .Callback(() => series[1].LastInfoSync = DateTime.Now);

            Mocker.GetMock<ISeriesRepository>()
                .Setup(s => s.Get(series[0].Id)).Returns(series[0]);

            Mocker.GetMock<ISeriesRepository>()
                .Setup(s => s.Get(series[1].Id)).Returns(series[1]);

            Mocker.GetMock<IMediaFileService>()
                .Setup(s => s.GetFilesBySeries(It.IsAny<int>())).Returns(new List<EpisodeFile>());

            
            Mocker.Resolve<ImportNewSeriesJob>().Start(notification, null);

            
            Mocker.GetMock<DiskScanJob>().Verify(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)), Times.Once());
            Mocker.GetMock<DiskScanJob>().Verify(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[1].Id)), Times.Once());

            Mocker.GetMock<UpdateInfoJob>().Verify(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)), Times.Once());
            Mocker.GetMock<UpdateInfoJob>().Verify(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[1].Id)), Times.Once());

        }




        [Test]
        [Timeout(3000)]
        public void failed_import_should_not_be_stuck_in_loop()
        {
            var series = Builder<Series>.CreateListOfSize(2)
                     .All().With(s => s.LastInfoSync = null)
                     .TheFirst(1).With(s => s.Id = 12)
                     .TheNext(1).With(s => s.Id = 15)
                        .Build();

            var notification = new ProgressNotification("Test");

            WithStrictMocker();

            Mocker.GetMock<ISeriesRepository>()
                .Setup(p => p.All())
                .Returns(series);

            Mocker.GetMock<UpdateInfoJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)))
                .Callback(() => series[0].LastInfoSync = DateTime.Now);

            Mocker.GetMock<UpdateInfoJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[1].Id)))
                .Throws(new InvalidOperationException());

            Mocker.GetMock<DiskScanJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)))
                .Callback(() => series[0].LastDiskSync = DateTime.Now);

            Mocker.GetMock<ISeriesRepository>()
                .Setup(s => s.Get(series[0].Id)).Returns(series[0]);

            Mocker.GetMock<IMediaFileService>()
                .Setup(s => s.GetFilesBySeries(It.IsAny<int>())).Returns(new List<EpisodeFile>());

            Mocker.GetMock<XemUpdateJob>()
                .Setup(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)));

            
            Mocker.Resolve<ImportNewSeriesJob>().Start(notification, null);

            
            Mocker.GetMock<UpdateInfoJob>().Verify(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)), Times.Once());
            Mocker.GetMock<UpdateInfoJob>().Verify(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[1].Id)), Times.Once());

            Mocker.GetMock<DiskScanJob>().Verify(j => j.Start(notification, It.Is<object>(d => d.GetPropertyValue<int>("SeriesId") == series[0].Id)), Times.Once());

            ExceptionVerification.ExpectedErrors(1);

        }



        [Test]
        public void AutoIgnoreSeason_new_series_should_not_ignore_any()
        {
            int seriesId = 12;

            WithStrictMocker();
            Mocker.GetMock<IMediaFileService>()
                .Setup(p => p.GetFilesBySeries(seriesId))
                .Returns(new List<EpisodeFile>());

            Mocker.GetMock<ISeasonRepository>()
                .Setup(p => p.GetSeasonNumbers(seriesId))
                .Returns(new List<int> { 0, 1, 2, 3, 4 });

            Mocker.Resolve<ImportNewSeriesJob>().AutoIgnoreSeasons(seriesId);


            Mocker.GetMock<ISeasonService>().Verify(p => p.SetIgnore(seriesId, It.IsAny<int>(), It.IsAny<Boolean>()), Times.Never());
        }

        [Test]
        public void AutoIgnoreSeason_existing_should_not_ignore_currentseason()
        {
            int seriesId = 12;

            var episodesFiles = Builder<EpisodeFile>.CreateListOfSize(2)
            .All().With(e => e.SeriesId = seriesId)
            .Build();

            episodesFiles[0].SeasonNumber = 0;
            episodesFiles[1].SeasonNumber = 1;

            WithStrictMocker();

            Mocker.GetMock<IMediaFileService>()
                .Setup(p => p.GetFilesBySeries(seriesId))
                .Returns(episodesFiles.ToList());

            Mocker.GetMock<ISeasonRepository>()
                .Setup(p => p.GetSeasonNumbers(seriesId))
                .Returns(new List<int> { 0, 1, 2 });

            Mocker.Resolve<ImportNewSeriesJob>().AutoIgnoreSeasons(seriesId);

            Mocker.GetMock<ISeasonService>().Verify(p => p.SetIgnore(seriesId, 2, It.IsAny<Boolean>()), Times.Never());
        }

        [Test]
        public void AutoIgnoreSeason_existing_should_ignore_seasons_with_no_file()
        {
            int seriesId = 12;

            var episodesFiles = Builder<EpisodeFile>.CreateListOfSize(2)
            .All().With(e => e.SeriesId = seriesId)
            .Build();

            episodesFiles[0].SeasonNumber = 1;

            

            Mocker.GetMock<IMediaFileService>()
                .Setup(p => p.GetFilesBySeries(seriesId))
                .Returns(episodesFiles.ToList());

            Mocker.GetMock<ISeasonRepository>()
                .Setup(p => p.GetSeasonNumbers(seriesId))
                .Returns(new List<int> { 0, 1, 2 });

            Mocker.Resolve<ImportNewSeriesJob>().AutoIgnoreSeasons(seriesId);

            Mocker.GetMock<ISeasonService>().Verify(p => p.SetIgnore(seriesId, 0, true), Times.Once());
            Mocker.GetMock<ISeasonService>().Verify(p => p.SetIgnore(seriesId, 1, true), Times.Never());
            Mocker.GetMock<ISeasonService>().Verify(p => p.SetIgnore(seriesId, 2, It.IsAny<Boolean>()), Times.Never());
        }
    }


}