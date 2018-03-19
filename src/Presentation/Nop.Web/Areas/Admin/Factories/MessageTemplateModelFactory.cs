﻿using System;
using System.Linq;
using Nop.Core.Domain.Messages;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Areas.Admin.Extensions;
using Nop.Web.Areas.Admin.Models.Messages;
using Nop.Web.Framework.Extensions;
using Nop.Web.Framework.Factories;

namespace Nop.Web.Areas.Admin.Factories
{
    /// <summary>
    /// Represents the message template model factory implementation
    /// </summary>
    public partial class MessageTemplateModelFactory : IMessageTemplateModelFactory
    {
        #region Fields

        private readonly IBaseAdminModelFactory _baseAdminModelFactory;
        private readonly ILocalizationService _localizationService;
        private readonly ILocalizedModelFactory _localizedModelFactory;
        private readonly IMessageTemplateService _messageTemplateService;
        private readonly IMessageTokenProvider _messageTokenProvider;
        private readonly IStoreMappingSupportedModelFactory _storeMappingSupportedModelFactory;
        private readonly IStoreService _storeService;

        #endregion

        #region Ctor

        public MessageTemplateModelFactory(IBaseAdminModelFactory baseAdminModelFactory,
            ILocalizationService localizationService,
            ILocalizedModelFactory localizedModelFactory,
            IMessageTemplateService messageTemplateService,
            IMessageTokenProvider messageTokenProvider,
            IStoreMappingSupportedModelFactory storeMappingSupportedModelFactory,
            IStoreService storeService)
        {
            this._baseAdminModelFactory = baseAdminModelFactory;
            this._localizationService = localizationService;
            this._localizedModelFactory = localizedModelFactory;
            this._messageTemplateService = messageTemplateService;
            this._messageTokenProvider = messageTokenProvider;
            this._storeMappingSupportedModelFactory = storeMappingSupportedModelFactory;
            this._storeService = storeService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Prepare message template search model
        /// </summary>
        /// <param name="model">Message template search model</param>
        /// <returns>Message template search model</returns>
        public virtual MessageTemplateSearchModel PrepareMessageTemplateSearchModel(MessageTemplateSearchModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            //prepare available stores
            _baseAdminModelFactory.PrepareStores(model.AvailableStores);

            return model;
        }

        /// <summary>
        /// Prepare paged message template list model
        /// </summary>
        /// <param name="searchModel">Message template search model</param>
        /// <returns>Message template list model</returns>
        public virtual MessageTemplateListModel PrepareMessageTemplateListModel(MessageTemplateSearchModel searchModel)
        {
            if (searchModel == null)
                throw new ArgumentNullException(nameof(searchModel));

            //get message templates
            var messageTemplates = _messageTemplateService.GetAllMessageTemplates(storeId: searchModel.SearchStoreId);
            
            //prepare store names (to avoid loading for each message template)
            var stores = _storeService.GetAllStores().Select(store => new { store.Id, store.Name });

            //prepare list model
            var model = new MessageTemplateListModel
            {
                Data = messageTemplates.PaginationByRequestModel(searchModel).Select(messageTemplate =>
                {
                    //fill in model values from the entity
                    var messageTemplateModel = messageTemplate.ToModel();

                    //fill in additional values (not existing in the entity)
                    var storeNames = stores.Select(store => store.Name);
                    if (messageTemplate.LimitedToStores)
                    {
                        _storeMappingSupportedModelFactory.PrepareModelStores(messageTemplateModel, messageTemplate, false);
                        storeNames = stores
                            .Where(store => messageTemplateModel.SelectedStoreIds.Contains(store.Id)).Select(store => store.Name);
                    }                    
                    messageTemplateModel.ListOfStores = string.Join(", ", storeNames);

                    return messageTemplateModel;
                }),
                Total = messageTemplates.Count
            };

            return model;
        }

        /// <summary>
        /// Prepare message template model
        /// </summary>
        /// <param name="model">Message template model</param>
        /// <param name="messageTemplate">Message template</param>
        /// <param name="excludeProperties">Whether to exclude populating of some properties of model</param>
        /// <returns>Message template model</returns>
        public virtual MessageTemplateModel PrepareMessageTemplateModel(MessageTemplateModel model,
            MessageTemplate messageTemplate, bool excludeProperties = false)
        {
            Action<MessageTemplateLocalizedModel, int> localizedModelConfiguration = null;

            if (messageTemplate != null)
            {
                //fill in model values from the entity
                model = model ?? messageTemplate.ToModel();

                //define localized model configuration action
                localizedModelConfiguration = (locale, languageId) =>
                {
                    locale.BccEmailAddresses = messageTemplate.GetLocalized(entity => entity.BccEmailAddresses, languageId, false, false);
                    locale.Subject = messageTemplate.GetLocalized(entity => entity.Subject, languageId, false, false);
                    locale.Body = messageTemplate.GetLocalized(entity => entity.Body, languageId, false, false);
                    locale.EmailAccountId = messageTemplate.GetLocalized(entity => entity.EmailAccountId, languageId, false, false);

                    //prepare available email accounts
                    _baseAdminModelFactory.PrepareEmailAccounts(locale.AvailableEmailAccounts,
                        defaultItemText: _localizationService.GetResource("Admin.ContentManagement.MessageTemplates.Fields.EmailAccount.Standard"));
                };
            }

            model.SendImmediately = !model.DelayBeforeSend.HasValue;
            model.HasAttachedDownload = model.AttachedDownloadId > 0;

            var allowedTokens = string.Join(", ", _messageTokenProvider.GetListOfAllowedTokens(messageTemplate.GetTokenGroups()));
            model.AllowedTokens = $"{allowedTokens}{Environment.NewLine}{Environment.NewLine}" +
                $"{_localizationService.GetResource("Admin.ContentManagement.MessageTemplates.Tokens.ConditionalStatement")}{Environment.NewLine}";

            //prepare localized models
            if (!excludeProperties)
                model.Locales = _localizedModelFactory.PrepareLocalizedModels(localizedModelConfiguration);

            //prepare available email accounts
            _baseAdminModelFactory.PrepareEmailAccounts(model.AvailableEmailAccounts);

            //prepare available stores
            _storeMappingSupportedModelFactory.PrepareModelStores(model, messageTemplate, excludeProperties);

            return model;
        }

        /// <summary>
        /// Prepare test message template model
        /// </summary>
        /// <param name="model">Test message template model</param>
        /// <param name="messageTemplate">Message template</param>
        /// <param name="languageId">Language identifier</param>
        /// <returns>Test message template model</returns>
        public virtual TestMessageTemplateModel PrepareTestMessageTemplateModel(TestMessageTemplateModel model,
            MessageTemplate messageTemplate, int languageId)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            if (messageTemplate == null)
                throw new ArgumentNullException(nameof(messageTemplate));

            model.Id = messageTemplate.Id;
            model.LanguageId = languageId;

            //filter tokens to the current template
            var subject = messageTemplate.GetLocalized(entity => entity.Subject, languageId);
            var body = messageTemplate.GetLocalized(entity => entity.Body, languageId);
            model.Tokens = _messageTokenProvider.GetListOfAllowedTokens()
                .Where(token => subject.Contains(token) || body.Contains(token)).ToList();

            return model;
        }

        #endregion
    }
}