﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using AasxIntegrationBase;
using AasxPredefinedConcepts;
using AdminShellNS;

namespace AasxPluginDocumentShelf
{
    /// <summary>
    /// Representation of a VDI2770 Document or so ..
    /// </summary>
    public class DocumentEntity
    {
        public delegate void DocumentEntityEvent(DocumentEntity e);
        public event DocumentEntityEvent DoubleClick = null;

        public delegate void MenuClickDelegate(DocumentEntity e, string menuItemHeader, object tag);
        public event MenuClickDelegate MenuClick = null;

        public enum SubmodelVersion { Default, V11 }

        public SubmodelVersion SmVersion = SubmodelVersion.Default;

        public string Title = "";
        public string Organization = "";
        public string FurtherInfo = "";
        public string[] CountryCodes;
        public string DigitalFile = "";
        public System.Windows.Controls.Viewbox ImgContainer = null;
        public string ReferableHash = null;

        public AdminShell.SubmodelElementWrapperCollection SourceElementsDocument = null;
        public AdminShell.SubmodelElementWrapperCollection SourceElementsDocumentVersion = null;

        public string ImageReadyToBeLoaded = null; // adding Image to ImgContainer needs to be done by the GUI thread!!
        public string[] DeleteFilesAfterLoading = null;

        public enum DocRelationType { DocumentedEntity, RefersTo, BasedOn, Affecting, TranslationOf };
        public List<Tuple<DocRelationType, AdminShell.Reference>> Relations =
            new List<Tuple<DocRelationType, AdminShellV20.Reference>>();

        public DocumentEntity() { }

        public DocumentEntity(string Title, string Organization, string FurtherInfo, string[] LangCodes = null)
        {
            this.Title = Title;
            this.Organization = Organization;
            this.FurtherInfo = FurtherInfo;
            this.CountryCodes = LangCodes;
        }

        public void RaiseDoubleClick()
        {
            if (DoubleClick != null)
                DoubleClick(this);
        }

        public void RaiseMenuClick(string menuItemHeader, object tag)
        {
            if (MenuClick != null)
                MenuClick(this, menuItemHeader, tag);
        }
    }

    public class ListOfDocumentEntity : List<DocumentEntity>
    {
        //
        // Default
        //

        public static ListOfDocumentEntity ParseSubmodelForV10(
            AdminShellPackageEnv thePackage,
            AdminShell.Submodel subModel, AasxPluginDocumentShelf.DocumentShelfOptions options,
            string defaultLang,
            int selectedDocClass, AasxLanguageHelper.LangEnum selectedLanguage)
        {
            // set a new list
            var its = new ListOfDocumentEntity();

            // look for Documents
            if (subModel?.submodelElements != null)
                foreach (var smcDoc in
                    subModel.submodelElements.FindAllSemanticIdAs<AdminShell.SubmodelElementCollection>(
                        options?.SemIdDocument, AdminShellV20.Key.MatchMode.Relaxed))
                {
                    // access
                    if (smcDoc == null || smcDoc.value == null)
                        continue;

                    // look immediately for DocumentVersion, as only with this there is a valid List item
                    foreach (var smcVer in
                        smcDoc.value.FindAllSemanticIdAs<AdminShell.SubmodelElementCollection>(
                            options?.SemIdDocumentVersion, AdminShellV20.Key.MatchMode.Relaxed))
                    {
                        // access
                        if (smcVer == null || smcVer.value == null)
                            continue;

                        //
                        // try to lookup info in smcDoc and smcVer
                        //

                        // take the 1st title
                        var title =
                            "" +
                            smcVer.value.FindFirstSemanticIdAs<AdminShell.Property>(options?.SemIdTitle,
                            AdminShellV20.Key.MatchMode.Relaxed)?.value;

                        // could be also a multi-language title
                        foreach (var mlp in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.MultiLanguageProperty>(
                                options?.SemIdTitle, AdminShellV20.Key.MatchMode.Relaxed))
                            if (mlp.value != null)
                                title = mlp.value.GetDefaultStr(defaultLang);

                        // have multiple opportunities for orga
                        var orga =
                            "" +
                            smcVer.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                options?.SemIdOrganizationOfficialName, AdminShellV20.Key.MatchMode.Relaxed)?.value;
                        if (orga.Trim().Length < 1)
                            orga =
                                "" +
                                smcVer.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                    options?.SemIdOrganizationName, AdminShellV20.Key.MatchMode.Relaxed)?.value;

                        // class infos
                        var classId =
                            "" +
                            smcDoc.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                options?.SemIdDocumentClassId, AdminShellV20.Key.MatchMode.Relaxed)?.value;

                        // collect country codes
                        var countryCodesStr = new List<string>();
                        var countryCodesEnum = new List<AasxLanguageHelper.LangEnum>();
                        foreach (var cclp in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.Property>(options?.SemIdLanguage,
                            AdminShellV20.Key.MatchMode.Relaxed))
                        {
                            // language code
                            var candidate = "" + cclp.value;
                            if (candidate.Length < 1)
                                continue;

                            // convert to country codes and add
                            var le = AasxLanguageHelper.FindLangEnumFromLangCode(candidate);
                            if (le != AasxLanguageHelper.LangEnum.Any)
                            {
                                countryCodesEnum.Add(le);
                                countryCodesStr.Add(AasxLanguageHelper.GetCountryCodeFromEnum(le));
                            }
                        }

                        // evaluate, if in selection
                        var okDocClass =
                            (selectedDocClass < 1 || classId == null || classId.Trim().Length < 1 ||
                            classId.Trim()
                                .StartsWith(
                                    DefinitionsVDI2770.GetDocClass(
                                        (DefinitionsVDI2770.Vdi2770DocClass)selectedDocClass)));

                        var okLanguage =
                            (selectedLanguage == AasxLanguageHelper.LangEnum.Any ||
                            countryCodesEnum == null ||
                            // make only exception, if no language not all (not only the preferred
                            // of LanguageSelectionToISO639String) are in the property
                            countryCodesStr.Count < 1 ||
                            countryCodesEnum.Contains(selectedLanguage));

                        if (!okDocClass || !okLanguage)
                            continue;

                        // further info
                        var further = "";
                        foreach (var fi in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.Property>(
                                options?.SemIdDocumentVersionIdValue))
                            further += "\u00b7 version: " + fi.value;
                        foreach (var fi in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.Property>(options?.SemIdDate,
                            AdminShellV20.Key.MatchMode.Relaxed))
                            further += "\u00b7 date: " + fi.value;
                        if (further.Length > 0)
                            further = further.Substring(2);

                        // construct entity
                        var ent = new DocumentEntity(title, orga, further, countryCodesStr.ToArray());
                        ent.ReferableHash = String.Format(
                            "{0:X14} {1:X14}", thePackage.GetHashCode(), smcDoc.GetHashCode());

                        // for updating data, set the source elements of this document entity
                        ent.SourceElementsDocument = smcDoc.value;
                        ent.SourceElementsDocumentVersion = smcVer.value;

                        // filename
                        var fn = smcVer.value.FindFirstSemanticIdAs<AdminShell.File>(
                            options?.SemIdDigitalFile, AdminShellV20.Key.MatchMode.Relaxed)?.value;
                        ent.DigitalFile = fn;

                        // add
                        ent.SmVersion = DocumentEntity.SubmodelVersion.Default;
                        its.Add(ent);
                    }
                }

            // ok
            return its;
        }

        //
        // V11
        //

        private static void SeachForRelations(
            AdminShell.SubmodelElementWrapperCollection smwc,
            DocumentEntity.DocRelationType drt,
            AdminShell.Reference semId,
            DocumentEntity intoDoc)
        {
            // access
            if (smwc == null || semId == null || intoDoc == null)
                return;

            foreach (var re in smwc.FindAllSemanticIdAs<AdminShell.ReferenceElement>(semId,
                AdminShellV20.Key.MatchMode.Relaxed))
            {
                // access 
                if (re.value == null || re.value.Count < 1)
                    continue;

                // be a bit picky
                if (re.value.Last.type.ToLower().Trim() != AdminShell.Key.Entity.ToLower())
                    continue;

                // add
                intoDoc.Relations.Add(new Tuple<DocumentEntity.DocRelationType, AdminShellV20.Reference>(
                    drt, re.value));
            }
        }

        public static ListOfDocumentEntity ParseSubmodelForV11(
            AdminShellPackageEnv thePackage,
            AdminShell.Submodel subModel, AasxPredefinedConcepts.VDI2770v11 defs11,
            string defaultLang,
            int selectedDocClass, AasxLanguageHelper.LangEnum selectedLanguage)
        {
            // set a new list
            var its = new ListOfDocumentEntity();
            if (thePackage == null || subModel == null || defs11 == null)
                return its;

            // look for Documents
            if (subModel.submodelElements != null)
                foreach (var smcDoc in
                    subModel.submodelElements.FindAllSemanticIdAs<AdminShell.SubmodelElementCollection>(
                        defs11.CD_Document?.GetReference(), AdminShellV20.Key.MatchMode.Relaxed))
                {
                    // access
                    if (smcDoc == null || smcDoc.value == null)
                        continue;

                    // look immediately for DocumentVersion, as only with this there is a valid List item
                    foreach (var smcVer in
                        smcDoc.value.FindAllSemanticIdAs<AdminShell.SubmodelElementCollection>(
                            defs11.CD_DocumentVersion?.GetReference(), AdminShellV20.Key.MatchMode.Relaxed))
                    {
                        // access
                        if (smcVer == null || smcVer.value == null)
                            continue;

                        //
                        // try to lookup info in smcDoc and smcVer
                        //

                        // take the 1st title
                        var title = "" + smcVer.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                defs11.CD_Title?.GetReference(), AdminShellV20.Key.MatchMode.Relaxed)?.value;

                        // could be also a multi-language title
                        foreach (var mlp in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.MultiLanguageProperty>(
                                defs11.CD_Title?.GetReference(), AdminShellV20.Key.MatchMode.Relaxed))
                            if (mlp.value != null)
                                title = mlp.value.GetDefaultStr(defaultLang);

                        // have multiple opportunities for orga
                        var orga = "" + smcVer.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                defs11.CD_OrganizationOfficialName?.GetReference(),
                                AdminShellV20.Key.MatchMode.Relaxed)?.value;
                        if (orga.Trim().Length < 1)
                            orga = "" + smcVer.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                    defs11.CD_OrganizationName?.GetReference(),
                                    AdminShellV20.Key.MatchMode.Relaxed)?.value;

                        // try find language
                        // collect country codes
                        var countryCodesStr = new List<string>();
                        var countryCodesEnum = new List<AasxLanguageHelper.LangEnum>();
                        foreach (var cclp in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.Property>(defs11.CD_Language?.GetReference(),
                            AdminShellV20.Key.MatchMode.Relaxed))
                        {
                            // language code
                            var candidate = "" + cclp.value;
                            if (candidate.Length < 1)
                                continue;

                            // convert to country codes and add
                            var le = AasxLanguageHelper.FindLangEnumFromLangCode(candidate);
                            if (le != AasxLanguageHelper.LangEnum.Any)
                            {
                                countryCodesEnum.Add(le);
                                countryCodesStr.Add(AasxLanguageHelper.GetCountryCodeFromEnum(le));
                            }
                        }

                        var okLanguage =
                            (selectedLanguage == AasxLanguageHelper.LangEnum.Any ||
                            countryCodesEnum == null ||
                            // make only exception, if no language not all (not only the preferred
                            // of LanguageSelectionToISO639String) are in the property
                            countryCodesStr.Count < 1 ||
                            countryCodesEnum.Contains(selectedLanguage));

                        // try find a 2770 classification
                        var okDocClass = false;
                        foreach (var smcClass in
                            smcDoc.value.FindAllSemanticIdAs<AdminShell.SubmodelElementCollection>(
                                defs11.CD_DocumentClassification?.GetReference(), AdminShellV20.Key.MatchMode.Relaxed))
                        {
                            // access
                            if (smcClass?.value == null)
                                continue;

                            // shall be a 2770 classification
                            var classSys = "" + smcVer.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                    defs11.CD_DocumentClassificationSystem?.GetReference(),
                                    AdminShellV20.Key.MatchMode.Relaxed)?.value;
                            if (classSys.ToLower().Trim() != DefinitionsVDI2770.Vdi2770Sys.ToLower())
                                continue;

                            // class infos
                            var classId = "" + smcDoc.value.FindFirstSemanticIdAs<AdminShell.Property>(
                                    defs11.CD_DocumentClassId?.GetReference(),
                                    AdminShellV20.Key.MatchMode.Relaxed)?.value;

                            // evaluate, if in selection
                            okDocClass = okDocClass ||
                                (classId.Trim().Length < 1 ||
                                classId.Trim()
                                    .StartsWith(
                                        DefinitionsVDI2770.GetDocClass(
                                            (DefinitionsVDI2770.Vdi2770DocClass)selectedDocClass)));

                        }

                        // success for selections?
                        if (!(selectedDocClass < 1 || okDocClass) || !okLanguage)
                            continue;

                        // further info
                        var further = "";
                        foreach (var fi in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.Property>(
                                defs11.CD_DocumentVersionIdValue?.GetReference()))
                            further += "\u00b7 version: " + fi.value;
                        foreach (var fi in
                            smcVer.value.FindAllSemanticIdAs<AdminShell.Property>(defs11.CD_Date?.GetReference(),
                            AdminShellV20.Key.MatchMode.Relaxed))
                            further += "\u00b7 date: " + fi.value;
                        if (further.Length > 0)
                            further = further.Substring(2);

                        // construct entity
                        var ent = new DocumentEntity(title, orga, further, countryCodesStr.ToArray());
                        ent.ReferableHash = String.Format(
                            "{0:X14} {1:X14}", thePackage.GetHashCode(), smcDoc.GetHashCode());

                        // for updating data, set the source elements of this document entity
                        ent.SourceElementsDocument = smcDoc.value;
                        ent.SourceElementsDocumentVersion = smcVer.value;

                        // filename
                        var fn = smcVer.value.FindFirstSemanticIdAs<AdminShell.File>(
                            defs11.CD_DigitalFile?.GetReference())?.value;
                        ent.DigitalFile = fn;

                        // relations
                        SeachForRelations(smcDoc.value, DocumentEntity.DocRelationType.DocumentedEntity,
                            defs11.CD_DocumentedEntity?.GetReference(), ent);
                        SeachForRelations(smcDoc.value, DocumentEntity.DocRelationType.RefersTo,
                            defs11.CD_RefersTo?.GetReference(), ent);
                        SeachForRelations(smcDoc.value, DocumentEntity.DocRelationType.BasedOn,
                            defs11.CD_BasedOn?.GetReference(), ent);
                        SeachForRelations(smcDoc.value, DocumentEntity.DocRelationType.Affecting,
                            defs11.CD_Affecting?.GetReference(), ent);
                        SeachForRelations(smcDoc.value, DocumentEntity.DocRelationType.TranslationOf,
                            defs11.CD_TranslationOf?.GetReference(), ent);

                        // add
                        ent.SmVersion = DocumentEntity.SubmodelVersion.V11;
                        its.Add(ent);
                    }
                }

            // ok
            return its;
        }

    }
}
