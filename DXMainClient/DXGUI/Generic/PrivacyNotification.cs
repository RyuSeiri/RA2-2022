﻿using ClientCore;
using ClientGUI;
using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System.Diagnostics;

namespace DTAClient.DXGUI.Generic
{
    /// <summary>
    /// A notification that asks the user to accept the CnCNet privacy policy.
    /// </summary>
    class PrivacyNotification : XNAWindow
    {
        public PrivacyNotification(WindowManager windowManager) : base(windowManager)
        {
            // DrawMode = ControlDrawMode.UNIQUE_RENDER_TARGET;
        }

        public override void Initialize()
        {
            Name = nameof(PrivacyNotification);
            Width = WindowManager.RenderResolutionX;

            var lblDescription = new XNALabel(WindowManager);
            lblDescription.Name = nameof(lblDescription);
            lblDescription.X = UIDesignConstants.EMPTY_SPACE_SIDES;
            lblDescription.Y = UIDesignConstants.EMPTY_SPACE_TOP;
            lblDescription.Text = Renderer.FixText("使用本客户端即代表您同意<<CnCNet条约和条款>>以及<<CnCNet隐私策略>>.有关隐私的选项可以在客户端的设置中调整",
                lblDescription.FontIndex, WindowManager.RenderResolutionX - (UIDesignConstants.EMPTY_SPACE_SIDES * 2)).Text;
            AddChild(lblDescription);

            var lblMoreInformation = new XNALabel(WindowManager);
            lblMoreInformation.Name = nameof(lblMoreInformation);
            lblMoreInformation.X = lblDescription.X;
            lblMoreInformation.Y = lblDescription.Bottom + UIDesignConstants.CONTROL_VERTICAL_MARGIN;
            lblMoreInformation.Text = "更多信息: ";
            AddChild(lblMoreInformation);

            var lblTermsAndConditions = new XNALinkLabel(WindowManager);
            lblTermsAndConditions.Name = nameof(lblTermsAndConditions);
            ////lblTermsAndConditions.X = lblMoreInformation.Right + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            lblTermsAndConditions.Y = lblMoreInformation.Y;
            lblTermsAndConditions.Text = "https://cncnet.org/terms-and-conditions";
            lblTermsAndConditions.LeftClick += (s, e) => Process.Start(lblTermsAndConditions.Text);
            AddChild(lblTermsAndConditions);

            var lblPrivacyPolicy = new XNALinkLabel(WindowManager);
            lblPrivacyPolicy.Name = nameof(lblPrivacyPolicy);
            lblPrivacyPolicy.X = lblTermsAndConditions.Right + UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            lblPrivacyPolicy.Y = lblMoreInformation.Y;
            lblPrivacyPolicy.Text = "https://cncnet.org/privacy-policy";
            lblPrivacyPolicy.LeftClick += (s, e) => Process.Start(lblPrivacyPolicy.Text);
            AddChild(lblPrivacyPolicy);

            var lblExplanation = new XNALabel(WindowManager);
            lblExplanation.Name = nameof(lblExplanation);
            lblExplanation.X = UIDesignConstants.EMPTY_SPACE_SIDES;
            lblExplanation.Y = lblMoreInformation.Bottom + UIDesignConstants.CONTROL_VERTICAL_MARGIN * 2;
            lblExplanation.Text = "No worries, we're not actually using your data for anything evil, but we have to display this message due to regulations.";
            lblExplanation.TextColor = UISettings.ActiveSettings.SubtleTextColor;
            AddChild(lblExplanation);

            var btnOK = new XNAClientButton(WindowManager);
            btnOK.Name = nameof(btnOK);
            btnOK.Width = 75;
            btnOK.Y = lblExplanation.Y;
            btnOK.X = WindowManager.RenderResolutionX - btnOK.Width - UIDesignConstants.CONTROL_HORIZONTAL_MARGIN;
            btnOK.Text = "同意";
            AddChild(btnOK);
            btnOK.LeftClick += (s, e) =>
            {
                UserINISettings.Instance.PrivacyPolicyAccepted.Value = true;
                UserINISettings.Instance.SaveSettings();
                // AlphaRate = -0.2f;
                Disable();
            };

            Height = btnOK.Bottom + UIDesignConstants.EMPTY_SPACE_BOTTOM;
            Y = WindowManager.RenderResolutionY - Height;

            base.Initialize();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Alpha <= 0.0)
                Disable();
        }
    }
}
