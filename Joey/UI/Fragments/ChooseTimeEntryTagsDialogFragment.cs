﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using GalaSoft.MvvmLight.Helpers;
using Toggl.Joey.UI.Utils;
using Toggl.Joey.UI.Views;
using Toggl.Phoebe.Data.Models;
using Toggl.Phoebe.Reactive;
using Toggl.Phoebe.ViewModels;

namespace Toggl.Joey.UI.Fragments
{
    public class ChooseTimeEntryTagsDialogFragment : BaseDialogFragment
    {
        private static readonly string SelectedTagNamesArgument = "com.toggl.timer.selected_tag_names";
        private static readonly string WorkspaceIdArgument = "com.toggl.timer.workspace_id";
        private ListView listView;
        private TagListVM viewModel;
        private IOnTagSelectedHandler updateTagHandler;

        private Guid WorkspaceId
        {
            get
            {
                Guid id;
                Guid.TryParse(Arguments.GetString(WorkspaceIdArgument), out id);
                return id;
            }
        }

        private IList<string> ExistingTags
        {
            get
            {
                return Arguments.GetStringArrayList(SelectedTagNamesArgument);
            }
        }

        public ChooseTimeEntryTagsDialogFragment()
        {
        }

        public ChooseTimeEntryTagsDialogFragment(IntPtr jref, Android.Runtime.JniHandleOwnership xfer) : base(jref, xfer)
        {
        }

        public static ChooseTimeEntryTagsDialogFragment NewInstance(Guid workspaceId, IReadOnlyList<string> tagStrings)
        {
            var fragment = new ChooseTimeEntryTagsDialogFragment();

            var args = new Bundle();
            args.PutString(WorkspaceIdArgument, workspaceId.ToString());
            args.PutStringArrayList(SelectedTagNamesArgument, tagStrings.ToList());
            fragment.Arguments = args;

            return fragment;
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            viewModel = new TagListVM(StoreManager.Singleton.AppState, WorkspaceId);
        }

        public override void OnDestroy()
        {
            viewModel.Dispose();
            base.OnDestroy();
        }

        public override Dialog OnCreateDialog(Bundle savedInstanceState)
        {
            // Mvvm ligth utility to generate an adapter from
            // an Observable collection. For the moment, use a dummy
            // adapter and repace it when the ViewModel is initializated.
            var tagsAdapter = new ObservableCollection<TagData> ().GetAdapter(GetTagView);

            var dia = new AlertDialog.Builder(Activity)
            .SetTitle(Resource.String.ChooseTimeEntryTagsDialogTitle)
            .SetAdapter(tagsAdapter, (IDialogInterfaceOnClickListener)null)
            .SetNeutralButton(Resource.String.ChooseTimeEntryTagsDialogCreate, OnCreateButtonClicked)
            .SetPositiveButton(Resource.String.ChooseTimeEntryTagsDialogOk, OnOkButtonClicked)
            .Create();

            listView = dia.ListView;
            listView.ItemsCanFocus = false;
            listView.ChoiceMode = ChoiceMode.Multiple;
            listView.ViewAttachedToWindow += (sender, e) => SetPreviousSelectedTags();

            return dia;
        }

        public ChooseTimeEntryTagsDialogFragment SetOnModifyTagListHandler(IOnTagSelectedHandler handler)
        {
            updateTagHandler = handler;
            return this;
        }

        private void OnCreateButtonClicked(object sender, DialogClickEventArgs args)
        {
            // Commit changes the user has made thusfar
            if (updateTagHandler != null)
            {
                updateTagHandler.OnModifyTagList(SelectedTags);
            }

            CreateTagDialogFragment.NewInstance(WorkspaceId)
            .SetCreateNewTagHandler(updateTagHandler)
            .Show(FragmentManager, "new_tag_dialog");

            Dismiss();
        }

        private void OnCancelButtonClicked(object sender, DialogClickEventArgs args)
        {
        }

        private void OnOkButtonClicked(object sender, DialogClickEventArgs args)
        {
            updateTagHandler.OnModifyTagList(SelectedTags);
        }

        private View GetTagView(int position, ITagData tagData, View convertView)
        {
            View view = convertView ?? LayoutInflater.FromContext(Activity).Inflate(Resource.Layout.TagListItem, null);
            var nameCheckedTextView = view.FindViewById<CheckedTextView> (Resource.Id.NameCheckedTextView).SetFont(Font.Roboto);
            nameCheckedTextView.Text = tagData.Name;
            return view;
        }

        private List<string> SelectedTags
        {
            get
            {
                return viewModel
                       .TagCollection
                       .Where((_, i) => listView.CheckedItemPositions.Get(i))
                       .Select(tag => tag.Name)
                       .ToList();
            }
        }

        private void SetPreviousSelectedTags()
        {
            if (listView == null || listView.Adapter == null || viewModel == null)
            {
                return;
            }

            // Set correct dialog Adapter
            var list = ExistingTags;
            listView.Adapter = viewModel.TagCollection.GetAdapter(GetTagView);
            listView.ClearChoices();

            for (int i = 0; i < viewModel.TagCollection.Count; i++)
            {
                if (list.Contains(viewModel.TagCollection [i].Name))
                {
                    listView.SetItemChecked(i, true);
                }
            }
        }
    }
}
