﻿using LINGYUN.Abp.Identity;
using LINGYUN.Abp.RealTime.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Emailing;
using Volo.Abp.Localization;

namespace LINGYUN.Abp.Notifications.Emailing;

public class EmailingNotificationPublishProvider : NotificationPublishProvider
{
    public const string ProviderName = NotificationProviderNames.Emailing;

    public override string Name => ProviderName;

    protected AbpLocalizationOptions LocalizationOptions { get; }

    protected IEmailSender EmailSender { get; }

    protected IStringLocalizerFactory LocalizerFactory { get; }

    protected IIdentityUserRepository UserRepository { get; }

    public EmailingNotificationPublishProvider(
        IEmailSender emailSender,
        IStringLocalizerFactory localizerFactory,
        IIdentityUserRepository userRepository,
        IOptions<AbpLocalizationOptions> localizationOptions)
    {
        EmailSender = emailSender;
        LocalizerFactory = localizerFactory;
        UserRepository = userRepository;

        LocalizationOptions = localizationOptions.Value;
    }

    protected async override Task PublishAsync(
        NotificationInfo notification,
        IEnumerable<UserIdentifier> identifiers,
        CancellationToken cancellationToken = default)
    {
        var userIds = identifiers.Select(x => x.UserId).ToList();
        var userList = await UserRepository.GetListByIdListAsync(userIds, cancellationToken: cancellationToken);
        var emailAddress = userList.Where(x => x.EmailConfirmed).Select(x => x.Email).Distinct().JoinAsString(",");

        if (emailAddress.IsNullOrWhiteSpace())
        {
            Logger.LogWarning("The subscriber did not confirm the email address and could not send email notifications!");
            return;
        }

        if (!notification.Data.NeedLocalizer())
        {
            var title = notification.Data.TryGetData("title").ToString();
            var message = notification.Data.TryGetData("message").ToString();

            await EmailSender.SendAsync(emailAddress, title, message);
        }
        else
        {
            var titleInfo = notification.Data.TryGetData("title").As<LocalizableStringInfo>();
            var titleLocalizer = await LocalizerFactory.CreateByResourceNameAsync(titleInfo.ResourceName);
            var title = titleLocalizer[titleInfo.Name, titleInfo.Values].Value;

            var messageInfo = notification.Data.TryGetData("message").As<LocalizableStringInfo>();
            var messageLocalizer = await LocalizerFactory.CreateByResourceNameAsync(messageInfo.ResourceName);
            var message = messageLocalizer[messageInfo.Name, messageInfo.Values].Value;

            await EmailSender.SendAsync(emailAddress, title, message);
        }
    }
}
