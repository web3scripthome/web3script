using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using web3script.Models;
using web3script.Services;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace web3script
{
    public class ProjectSelectDialog : Window
    {
        private ListBox projectListBox;
        
        public Project SelectedProject { get; private set; }
        
        public ProjectSelectDialog(List<Project> projects)
        {
            Title = "选择项目";
            Width = 400;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            
            var label = new TextBlock
            {
                Text = "请选择一个项目",
                Margin = new Thickness(10, 10, 10, 5),
                FontSize = 14
            };
            grid.Children.Add(label);
            Grid.SetRow(label, 0);
            
            projectListBox = new ListBox
            {
                Margin = new Thickness(10),
                DisplayMemberPath = "Name"
            };
            
            // 绑定项目列表
            projectListBox.ItemsSource = projects;
            
            // 如果有项目，默认选中第一个
            if (projects.Count > 0)
            {
                projectListBox.SelectedIndex = 0;
            }
            
            grid.Children.Add(projectListBox);
            Grid.SetRow(projectListBox, 1);
            
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnCancel.Click += (s, e) => { DialogResult = false; };
            
            var btnSelect = new Button
            {
                Content = "选择",
                Width = 80,
                Height = 30,
                IsDefault = true
            };
            btnSelect.Click += BtnSelect_Click;
            
            buttonPanel.Children.Add(btnCancel);
            buttonPanel.Children.Add(btnSelect);
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 2);
            
            Content = grid;
        }
        
        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (projectListBox.SelectedItem is Project selectedProject)
            {
                SelectedProject = selectedProject;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("请选择一个项目", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
} 
