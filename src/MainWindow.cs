using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace VPNTool
{
    public partial class MainWindow : Window
    {
        private TextBlock txtUninstStatus;
        private Border dotUninstStatus;
        private Border btnUninstAction;
        private TextBlock txtUninstBtn;

        private TextBlock txtInstStatus;
        private Border dotInstStatus;
        private Border btnInstAction;
        private TextBlock txtInstBtn;

        private Border toastNotification;
        private Border toastIconHolder;
        private TextBlock txtToastTitle;
        private TextBlock txtToastMessage;
        private System.Windows.Threading.DispatcherTimer toastTimer;

        public MainWindow()
        {
            this.Title = "GlobalProtect VPN Manager - Soporte Técnico";
            this.Width = 600;
            this.Height = 440;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ResizeMode = ResizeMode.NoResize;

            BuildUI();
            RefreshAllStates();
        }

        private void BuildUI()
        {
            // Borde exterior con sombra y bordes redondeados
            Border outerBorder = new Border
            {
                Background = UITheme.CreateSpaceBackground(),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(16),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 6,
                    Opacity = 0.65
                }
            };

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) }); // Barra de título
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Contenido (Tarjetas)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) }); // Barra inferior de pie

            // 1. Barra de Título personalizada
            Grid titleBar = new Grid { Background = Brushes.Transparent };

            // Área arrastrable (excluye deliberadamente los botones de la ventana para evitar capturar clics)
            Grid titleDragArea = new Grid { Background = Brushes.Transparent, Margin = new Thickness(0, 0, 70, 0) };
            titleDragArea.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };

            StackPanel titleLeft = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };

            // Icono del programa en título
            Border titleIconBox = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Margin = new Thickness(0, 0, 8, 0)
            };
            titleIconBox.Child = UITheme.CreateVectorIcon(UITheme.SvgLock, Brushes.White, 13);
            titleLeft.Children.Add(titleIconBox);

            TextBlock txtAppTitle = new TextBlock
            {
                Text = "VPN Manager",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleLeft.Children.Add(txtAppTitle);

            Border pillTag = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            pillTag.Child = new TextBlock
            {
                Text = "Soporte Automático",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                FontWeight = FontWeights.SemiBold
            };
            titleLeft.Children.Add(pillTag);

            titleDragArea.Children.Add(titleLeft);
            titleBar.Children.Add(titleDragArea);

            // Botones ventana (Minimizar / Cerrar) - colocados a la derecha
            StackPanel winBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            Border btnMin = CreateTitleButton("—", () => this.WindowState = WindowState.Minimized);
            Border btnClose = CreateTitleButton("✕", () => this.Close(), true);
            winBtns.Children.Add(btnMin);
            winBtns.Children.Add(btnClose);
            titleBar.Children.Add(winBtns);

            Grid.SetRow(titleBar, 0);
            mainGrid.Children.Add(titleBar);

            // 2. Contenedor Central con las 2 tarjetas
            Grid contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border cardUninstall = CreateUninstallCard();
            Grid.SetColumn(cardUninstall, 0);
            contentGrid.Children.Add(cardUninstall);

            Border cardInstall = CreateInstallCard();
            Grid.SetColumn(cardInstall, 1);
            contentGrid.Children.Add(cardInstall);

            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // 3. Barra inferior de pie
            Border footer = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(16, 0, 16, 0)
            };
            Grid footerGrid = new Grid();

            TextBlock txtFooter = new TextBlock
            {
                Text = "⚡ Acciones con cuenta de soporte local (.\\soporteneps) • Portabilidad 100%",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                VerticalAlignment = VerticalAlignment.Center
            };
            footerGrid.Children.Add(txtFooter);

            TextBlock txtCredits = new TextBlock
            {
                Text = "Autor: Carlos Barros",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            footerGrid.Children.Add(txtCredits);

            footer.Child = footerGrid;
            Grid.SetRow(footer, 2);
            mainGrid.Children.Add(footer);

            // Toast de notificación flotante sobre todo
            toastNotification = CreateToastBanner();
            Grid.SetRowSpan(toastNotification, 3);
            mainGrid.Children.Add(toastNotification);

            outerBorder.Child = mainGrid;
            this.Content = outerBorder;
        }

        #region TARJETA 1: DESINSTALAR VPN (GLOBALPROTECT + PALO ALTO)
        private Border CreateUninstallCard()
        {
            Border card = new Border
            {
                Width = 245,
                Height = 280,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(14, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Header < ::: > + badge
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) }); // Squircle icon
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Info & Status
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // Botón Desinstalar

            // 1. Header: < ::: >  |  Limpieza Total
            Grid header = new Grid();
            StackPanel navIcons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            navIcons.Children.Add(UITheme.CreateVectorIcon(UITheme.SvgChevronLeft, new SolidColorBrush(Color.FromRgb(148, 163, 184)), 11));
            Border dots = new Border { Margin = new Thickness(5, 0, 5, 0) };
            dots.Child = UITheme.CreateVectorIcon(UITheme.SvgDots, new SolidColorBrush(Color.FromRgb(148, 163, 184)), 11);
            navIcons.Children.Add(dots);
            navIcons.Children.Add(UITheme.CreateVectorIcon(UITheme.SvgChevronRight, new SolidColorBrush(Color.FromRgb(148, 163, 184)), 11));
            header.Children.Add(navIcons);

            Border badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 220, 38, 38)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 248, 113, 113)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = "Limpieza Total",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113))
            };
            header.Children.Add(badge);
            Grid.SetRow(header, 0);
            g.Children.Add(header);

            // 2. Icono Squircle Rojo con Papelera
            Border squircle = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = Color.FromRgb(220, 38, 38), BlurRadius = 14, ShadowDepth = 2, Opacity = 0.4 }
            };
            squircle.Child = UITheme.CreateVectorIcon(UITheme.SvgTrash, Brushes.White, 24);
            Grid.SetRow(squircle, 1);
            g.Children.Add(squircle);

            // 3. Título, subtítulo y estado
            StackPanel infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };

            TextBlock txtTitle = new TextBlock
            {
                Text = "Desinstalar VPN",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            infoStack.Children.Add(txtTitle);

            TextBlock txtSubtitle = new TextBlock
            {
                Text = "GlobalProtect + Palo Alto",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 6)
            };
            infoStack.Children.Add(txtSubtitle);

            StackPanel statusRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            dotUninstStatus = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(3.5),
                Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(dotUninstStatus);

            txtUninstStatus = new TextBlock
            {
                Text = "Comprobando...",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(txtUninstStatus);
            infoStack.Children.Add(statusRow);

            Grid.SetRow(infoStack, 2);
            g.Children.Add(infoStack);

            // 4. Botón Acción Desinstalar
            btnUninstAction = new Border
            {
                Height = 38,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Cursor = Cursors.Hand
            };
            StackPanel btnContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            btnContent.Children.Add(UITheme.CreateVectorIcon(UITheme.SvgTrash, Brushes.White, 14));
            txtUninstBtn = new TextBlock
            {
                Text = "Desinstalar",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(8, 0, 0, 0)
            };
            btnContent.Children.Add(txtUninstBtn);
            btnUninstAction.Child = btnContent;

            btnUninstAction.MouseEnter += (s, e) => btnUninstAction.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            btnUninstAction.MouseLeave += (s, e) => btnUninstAction.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            btnUninstAction.PreviewMouseLeftButtonDown += async (s, e) =>
            {
                e.Handled = true;
                await ExecuteUninstallAsync();
            };

            Grid.SetRow(btnUninstAction, 3);
            g.Children.Add(btnUninstAction);

            card.Child = g;
            return card;
        }
        #endregion

        #region TARJETA 2: INSTALAR VPN (GLOBALPROTECT 64-BIT)
        private Border CreateInstallCard()
        {
            Border card = new Border
            {
                Width = 245,
                Height = 280,
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(8, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) }); // Header < ::: > + badge x64
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) }); // Squircle icon
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Info & Status
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) }); // Botón Instalar

            // 1. Header: < ::: >  |  x64
            Grid header = new Grid();
            StackPanel navIcons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            navIcons.Children.Add(UITheme.CreateVectorIcon(UITheme.SvgChevronLeft, new SolidColorBrush(Color.FromRgb(148, 163, 184)), 11));
            Border dots = new Border { Margin = new Thickness(5, 0, 5, 0) };
            dots.Child = UITheme.CreateVectorIcon(UITheme.SvgDots, new SolidColorBrush(Color.FromRgb(148, 163, 184)), 11);
            navIcons.Children.Add(dots);
            navIcons.Children.Add(UITheme.CreateVectorIcon(UITheme.SvgChevronRight, new SolidColorBrush(Color.FromRgb(148, 163, 184)), 11));
            header.Children.Add(navIcons);

            Border badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 59, 130, 246)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 96, 165, 250)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = "x64",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250))
            };
            header.Children.Add(badge);
            Grid.SetRow(header, 0);
            g.Children.Add(header);

            // 2. Icono Squircle Azul con Candado
            Border squircle = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = Color.FromRgb(37, 99, 235), BlurRadius = 14, ShadowDepth = 2, Opacity = 0.45 }
            };
            squircle.Child = UITheme.CreateVectorIcon(UITheme.SvgLock, Brushes.White, 24);
            Grid.SetRow(squircle, 1);
            g.Children.Add(squircle);

            // 3. Título, subtítulo y estado
            StackPanel infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };

            TextBlock txtTitle = new TextBlock
            {
                Text = "VPN",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center
            };
            infoStack.Children.Add(txtTitle);

            TextBlock txtSubtitle = new TextBlock
            {
                Text = "GlobalProtect 64-bit",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 6)
            };
            infoStack.Children.Add(txtSubtitle);

            StackPanel statusRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            dotInstStatus = new Border
            {
                Width = 7,
                Height = 7,
                CornerRadius = new CornerRadius(3.5),
                Background = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(dotInstStatus);

            txtInstStatus = new TextBlock
            {
                Text = "Instalador por red",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Center
            };
            statusRow.Children.Add(txtInstStatus);
            infoStack.Children.Add(statusRow);

            Grid.SetRow(infoStack, 2);
            g.Children.Add(infoStack);

            // 4. Botón Acción Instalar
            btnInstAction = new Border
            {
                Height = 38,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)),
                Cursor = Cursors.Hand
            };
            StackPanel btnContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            btnContent.Children.Add(UITheme.CreateVectorIcon(UITheme.SvgDownload, Brushes.White, 14));
            txtInstBtn = new TextBlock
            {
                Text = "Instalar",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(8, 0, 0, 0)
            };
            btnContent.Children.Add(txtInstBtn);
            btnInstAction.Child = btnContent;

            btnInstAction.MouseEnter += (s, e) => btnInstAction.Background = new SolidColorBrush(Color.FromRgb(14, 165, 233));
            btnInstAction.MouseLeave += (s, e) => btnInstAction.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199));
            btnInstAction.PreviewMouseLeftButtonDown += async (s, e) =>
            {
                e.Handled = true;
                await ExecuteInstallAsync();
            };

            Grid.SetRow(btnInstAction, 3);
            g.Children.Add(btnInstAction);

            card.Child = g;
            return card;
        }
        #endregion

        #region LOGICA Y ACTUALIZACIÓN DE ESTADOS
        private void RefreshAllStates()
        {
            bool isInst = VpnManager.IsInstalled();
            bool hasFolders = VpnManager.HasUserFolders();
            string cachedMsi = VpnManager.GetCachedMsiPath();

            // Estado Desinstalación
            if (!isInst && !hasFolders)
            {
                dotUninstStatus.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                txtUninstStatus.Text = "Limpio • Sin rastros";
                txtUninstStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                txtUninstBtn.Text = "Reaplicar Limpieza";
            }
            else if (isInst && hasFolders)
            {
                dotUninstStatus.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                txtUninstStatus.Text = "VPN y carpeta Palo Alto";
                txtUninstStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                txtUninstBtn.Text = "Desinstalar";
            }
            else if (hasFolders)
            {
                dotUninstStatus.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                txtUninstStatus.Text = "Carpeta Palo Alto activa";
                txtUninstStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                txtUninstBtn.Text = "Limpiar Carpetas";
            }
            else
            {
                dotUninstStatus.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                txtUninstStatus.Text = "Info limpia • VPN en PC";
                txtUninstStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                txtUninstBtn.Text = "Desinstalar VPN";
            }

            // Estado Instalación
            if (isInst)
            {
                dotInstStatus.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                txtInstStatus.Text = "Instalado en el equipo";
                txtInstStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                txtInstBtn.Text = "Reinstalar VPN";
            }
            else if (!string.IsNullOrEmpty(cachedMsi))
            {
                dotInstStatus.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                txtInstStatus.Text = "Instalador en caché local";
                txtInstStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                txtInstBtn.Text = "Instalar";
            }
            else
            {
                dotInstStatus.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                txtInstStatus.Text = "Instalador por red";
                txtInstStatus.Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                txtInstBtn.Text = "Instalar";
            }
        }

        private async Task ExecuteUninstallAsync()
        {
            try
            {
                btnUninstAction.IsEnabled = false;
                txtUninstBtn.Text = "Desinstalando...";
                txtUninstStatus.Text = "Ejecutando desinstalación...";
                dotUninstStatus.Background = new SolidColorBrush(Color.FromRgb(56, 189, 248));

                ShowToast("Desinstalando VPN", "Ejecutando desinstalación y limpieza en segundo plano...");

                bool clean = await VpnManager.UninstallAndCleanAllAsync((step) =>
                {
                    Dispatcher.Invoke(() => txtUninstStatus.Text = step);
                });

                RefreshAllStates();
                btnUninstAction.IsEnabled = true;

                if (clean)
                {
                    ShowToast("Desinstalado con Éxito", "GlobalProtect y todos sus registros fueron eliminados del equipo.");
                }
                else
                {
                    ShowToast("Proceso Finalizado", "Se completó el proceso de desinstalación y limpieza.");
                }
            }
            catch (Exception ex)
            {
                while (ex is System.Reflection.TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                btnUninstAction.IsEnabled = true;
                RefreshAllStates();
                ShowToast("Error", "Error al desinstalar: " + ex.Message, true);
            }
        }

        private async Task ExecuteInstallAsync()
        {
            try
            {
                btnInstAction.IsEnabled = false;
                txtInstBtn.Text = "Instalando...";

                ShowToast("Instalando VPN", "Descargando e instalando en segundo plano...");

                bool ok = await VpnManager.InstallVpnAsync((percent, msg) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtInstStatus.Text = msg;
                    });
                });

                RefreshAllStates();
                btnInstAction.IsEnabled = true;

                if (ok)
                {
                    ShowToast("VPN Instalada", "GlobalProtect 64-bit quedó instalado y listo en el equipo.");
                }
                else
                {
                    ShowToast("Aviso", "No se pudo verificar la instalación de GlobalProtect.", true);
                }
            }
            catch (Exception ex)
            {
                while (ex is System.Reflection.TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                btnInstAction.IsEnabled = true;
                RefreshAllStates();
                ShowToast("Error", "No se pudo completar la instalación: " + ex.Message, true);
            }
        }
        #endregion

        #region TOAST BANNER FLOTANTE
        private Border CreateToastBanner()
        {
            Border b = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 48),
                Background = new SolidColorBrush(Color.FromArgb(245, 15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 8, 14, 8),
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 3, Opacity = 0.55 }
            };

            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
            toastIconHolder = new Border { VerticalAlignment = VerticalAlignment.Center };
            toastIconHolder.Child = UITheme.CreateVectorIcon(UITheme.SvgCheck, new SolidColorBrush(Color.FromRgb(34, 197, 94)), 15);
            sp.Children.Add(toastIconHolder);

            StackPanel textSp = new StackPanel { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            txtToastTitle = new TextBlock { FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            txtToastMessage = new TextBlock { FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)) };
            textSp.Children.Add(txtToastTitle);
            textSp.Children.Add(txtToastMessage);
            sp.Children.Add(textSp);

            b.Child = sp;

            toastTimer = new System.Windows.Threading.DispatcherTimer();
            toastTimer.Interval = TimeSpan.FromSeconds(5);
            toastTimer.Tick += (s, e) =>
            {
                toastTimer.Stop();
                b.Visibility = Visibility.Collapsed;
            };

            return b;
        }

        private void ShowToast(string title, string msg, bool isError = false)
        {
            if (toastNotification == null) return;
            toastTimer.Stop();

            if (isError)
            {
                toastIconHolder.Child = UITheme.CreateVectorIcon(UITheme.SvgAlert, new SolidColorBrush(Color.FromRgb(239, 68, 68)), 16);
                toastNotification.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 239, 68, 68));
                txtToastTitle.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            }
            else
            {
                toastIconHolder.Child = UITheme.CreateVectorIcon(UITheme.SvgCheck, new SolidColorBrush(Color.FromRgb(34, 197, 94)), 15);
                toastNotification.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 56, 189, 248));
                txtToastTitle.Foreground = Brushes.White;
            }

            txtToastTitle.Text = title;
            txtToastMessage.Text = msg;
            toastNotification.Visibility = Visibility.Visible;
            toastTimer.Start();
        }
        #endregion

        private Border CreateTitleButton(string text, Action onClick, bool isClose = false)
        {
            Border btn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0)
            };
            TextBlock txt = new TextBlock
            {
                Text = text,
                FontSize = isClose ? 11 : 13,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Child = txt;

            btn.MouseEnter += (s, e) =>
            {
                btn.Background = isClose ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
                txt.Foreground = Brushes.White;
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Background = Brushes.Transparent;
                txt.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            };

            btn.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };

            return btn;
        }
    }
}
