using Microsoft.VisualBasic.Activities;
using System;
using System.Activities;
using System.Activities.Presentation.Hosting;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RehostedDesigner.Port;

internal sealed class RehostedExpressionEditorService : IExpressionEditorService
{
    private readonly List<RehostedExpressionEditorInstance> instances = new();

    public IExpressionEditorInstance CreateExpressionEditor(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces, List<ModelItem> variables, string text, Type expressionType)
        => this.CreateExpressionEditor(assemblies, importedNamespaces, variables, text, expressionType, new Size(double.NaN, double.NaN));

    public IExpressionEditorInstance CreateExpressionEditor(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces, List<ModelItem> variables, string text, Type expressionType, Size initialSize)
        => this.CreateInstance(assemblies, importedNamespaces, variables, text, expressionType, initialSize);

    public IExpressionEditorInstance CreateExpressionEditor(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces, List<ModelItem> variables, string text)
        => this.CreateExpressionEditor(assemblies, importedNamespaces, variables, text, null, new Size(double.NaN, double.NaN));

    public IExpressionEditorInstance CreateExpressionEditor(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces, List<ModelItem> variables, string text, Size initialSize)
        => this.CreateExpressionEditor(assemblies, importedNamespaces, variables, text, null, initialSize);

    public void CloseExpressionEditors()
    {
        foreach (RehostedExpressionEditorInstance instance in this.instances.ToArray())
        {
            instance.Close();
        }

        this.instances.Clear();
    }

    public void UpdateContext(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces)
    {
        foreach (RehostedExpressionEditorInstance instance in this.instances)
        {
            instance.UpdateContext(assemblies, importedNamespaces);
        }
    }

    private IExpressionEditorInstance CreateInstance(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces, List<ModelItem> variables, string text, Type expressionType, Size initialSize)
    {
        RehostedExpressionEditorInstance instance = new RehostedExpressionEditorInstance(assemblies, importedNamespaces, variables, text, expressionType, initialSize);
        instance.Closing += (_, _) => this.instances.Remove(instance);
        this.instances.Add(instance);
        return instance;
    }
}

internal sealed class RehostedExpressionEditorInstance : UserControl, IExpressionEditorInstance
{
    private static readonly IReadOnlyList<string> Keywords =
    [
        "Nothing",
        "True",
        "False",
        "AndAlso",
        "OrElse",
        "Not",
        "If",
        "New",
        "Me",
    ];

    private readonly TextBox editor;
    private readonly Popup completionPopup;
    private readonly ListBox completionList;
    private readonly List<ModelItem> variables;
    private readonly Type expressionType;
    private bool isClosed;
    private bool suppressRefresh;
    private CompletionResponse activeCompletion;
    private RehostedCompletionCatalog completionCatalog;

    public RehostedExpressionEditorInstance(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces, List<ModelItem> variables, string text, Type expressionType, Size initialSize)
    {
        this.variables = variables ?? new List<ModelItem>();
        this.expressionType = expressionType;
        this.completionCatalog = new RehostedCompletionCatalog(assemblies, importedNamespaces, this.variables, Keywords);

        Grid root = new Grid();
        this.editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Text = text ?? string.Empty,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        if (!double.IsNaN(initialSize.Width) && initialSize.Width > 0)
        {
            this.editor.MinWidth = initialSize.Width;
        }

        if (!double.IsNaN(initialSize.Height) && initialSize.Height > 0)
        {
            this.editor.MinHeight = initialSize.Height;
        }

        this.completionList = new ListBox
        {
            DisplayMemberPath = nameof(CompletionEntry.DisplayText),
            IsTabStop = false,
            MaxHeight = 260,
            MinWidth = 220,
        };
        this.completionList.MouseDoubleClick += this.OnCompletionListMouseDoubleClick;
        this.completionList.PreviewMouseLeftButtonUp += this.OnCompletionListMouseLeftButtonUp;

        Border popupBorder = new Border
        {
            BorderBrush = Brushes.DimGray,
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Child = this.completionList,
        };

        this.completionPopup = new Popup
        {
            AllowsTransparency = true,
            Child = popupBorder,
            Placement = PlacementMode.Bottom,
            PlacementTarget = this.editor,
            StaysOpen = false,
        };
        this.completionPopup.Closed += (_, _) => this.activeCompletion = null;

        root.Children.Add(this.editor);
        this.Content = root;

        this.editor.TextChanged += this.OnEditorTextChanged;
        this.editor.PreviewKeyDown += this.OnEditorPreviewKeyDown;
        this.editor.GotKeyboardFocus += this.OnEditorGotKeyboardFocus;
        this.editor.LostKeyboardFocus += this.OnEditorLostKeyboardFocus;
        this.Unloaded += (_, _) => this.Close();
    }

    public Control HostControl => this;

    public string Text
    {
        get => this.editor.Text;
        set
        {
            string next = value ?? string.Empty;
            if (!string.Equals(this.editor.Text, next, StringComparison.Ordinal))
            {
                this.editor.Text = next;
            }
        }
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => this.editor.VerticalScrollBarVisibility;
        set => this.editor.VerticalScrollBarVisibility = value;
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => this.editor.HorizontalScrollBarVisibility;
        set => this.editor.HorizontalScrollBarVisibility = value;
    }

    public int MinLines
    {
        get => this.editor.MinLines;
        set => this.editor.MinLines = Math.Max(1, value);
    }

    public int MaxLines
    {
        get => this.editor.MaxLines;
        set => this.editor.MaxLines = value > 0 ? value : int.MaxValue;
    }

    public bool HasAggregateFocus
        => this.IsKeyboardFocusWithin || this.completionPopup.Child?.IsKeyboardFocusWithin == true;

    public bool AcceptsReturn
    {
        get => this.editor.AcceptsReturn;
        set => this.editor.AcceptsReturn = value;
    }

    public bool AcceptsTab
    {
        get => this.editor.AcceptsTab;
        set => this.editor.AcceptsTab = value;
    }

    public event EventHandler TextChanged;

    public event EventHandler LostAggregateFocus;

    public event EventHandler GotAggregateFocus;

    public event EventHandler Closing;

    public void UpdateContext(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces)
    {
        this.completionCatalog = new RehostedCompletionCatalog(assemblies, importedNamespaces, this.variables, Keywords);
        if (this.completionPopup.IsOpen)
        {
            this.RefreshCompletion(explicitRequest: true);
        }
    }

    public void Close()
    {
        if (this.isClosed)
        {
            return;
        }

        this.isClosed = true;
        this.CloseCompletion();
        this.Closing?.Invoke(this, EventArgs.Empty);
    }

    public new void Focus()
    {
        this.editor.Focus();
        this.editor.CaretIndex = this.editor.Text.Length;
    }

    public void ClearSelection()
        => this.editor.Select(this.editor.CaretIndex, 0);

    public bool Cut()
    {
        if (!this.CanCut())
        {
            return false;
        }

        this.editor.Cut();
        return true;
    }

    public bool Copy()
    {
        if (!this.CanCopy())
        {
            return false;
        }

        this.editor.Copy();
        return true;
    }

    public bool Paste()
    {
        if (!this.CanPaste())
        {
            return false;
        }

        this.editor.Paste();
        return true;
    }

    public bool Undo()
    {
        if (!this.CanUndo())
        {
            return false;
        }

        this.editor.Undo();
        return true;
    }

    public bool Redo()
    {
        if (!this.CanRedo())
        {
            return false;
        }

        this.editor.Redo();
        return true;
    }

    public bool CompleteWord()
    {
        if (this.completionPopup.IsOpen)
        {
            return this.CommitSelectedCompletion();
        }

        return this.RefreshCompletion(explicitRequest: true);
    }

    public bool GlobalIntellisense()
        => this.RefreshCompletion(explicitRequest: true);

    public bool ParameterInfo()
        => false;

    public bool QuickInfo()
        => false;

    public bool IncreaseFilterLevel()
        => false;

    public bool DecreaseFilterLevel()
        => false;

    public bool CanCut()
        => this.editor.SelectionLength > 0;

    public bool CanCopy()
        => this.editor.SelectionLength > 0;

    public bool CanPaste()
        => Clipboard.ContainsText();

    public bool CanUndo()
        => this.editor.CanUndo;

    public bool CanRedo()
        => this.editor.CanRedo;

    public bool CanCompleteWord()
        => true;

    public bool CanGlobalIntellisense()
        => true;

    public bool CanParameterInfo()
        => false;

    public bool CanQuickInfo()
        => false;

    public bool CanIncreaseFilterLevel()
        => false;

    public bool CanDecreaseFilterLevel()
        => false;

    public string GetCommittedText()
        => this.editor.Text;

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!this.suppressRefresh)
        {
            this.RefreshCompletion(explicitRequest: false);
        }

        this.TextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!this.completionPopup.IsOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                this.MoveSelection(+1);
                e.Handled = true;
                break;
            case Key.Up:
                this.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.PageDown:
                this.MoveSelection(+8);
                e.Handled = true;
                break;
            case Key.PageUp:
                this.MoveSelection(-8);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Tab:
                e.Handled = this.CommitSelectedCompletion();
                break;
            case Key.Escape:
                this.CloseCompletion();
                e.Handled = true;
                break;
        }
    }

    private void OnEditorGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => this.GotAggregateFocus?.Invoke(this, EventArgs.Empty);

    private void OnEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        this.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (!this.HasAggregateFocus)
            {
                this.CloseCompletion();
                this.LostAggregateFocus?.Invoke(this, EventArgs.Empty);
            }
        }));
    }

    private void OnCompletionListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (this.CommitSelectedCompletion())
        {
            e.Handled = true;
        }
    }

    private void OnCompletionListMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (this.CommitSelectedCompletion())
        {
            e.Handled = true;
        }
    }

    private bool RefreshCompletion(bool explicitRequest)
    {
        CompletionResponse completion = this.completionCatalog.GetCompletions(this.editor.Text, this.editor.CaretIndex, explicitRequest);
        if (completion is null || completion.Entries.Count == 0)
        {
            this.CloseCompletion();
            return false;
        }

        this.activeCompletion = completion;
        this.completionList.ItemsSource = completion.Entries;
        this.completionList.SelectedIndex = 0;
        this.completionPopup.IsOpen = true;
        return true;
    }

    private void CloseCompletion()
    {
        this.completionPopup.IsOpen = false;
        this.completionList.ItemsSource = null;
        this.activeCompletion = null;
    }

    private void MoveSelection(int delta)
    {
        if (this.completionList.Items.Count == 0)
        {
            return;
        }

        int nextIndex = this.completionList.SelectedIndex;
        if (nextIndex < 0)
        {
            nextIndex = 0;
        }
        else
        {
            nextIndex = Math.Max(0, Math.Min(this.completionList.Items.Count - 1, nextIndex + delta));
        }

        this.completionList.SelectedIndex = nextIndex;
        this.completionList.ScrollIntoView(this.completionList.SelectedItem);
    }

    private bool CommitSelectedCompletion()
    {
        CompletionEntry selected = this.completionList.SelectedItem as CompletionEntry;
        if (selected is null || this.activeCompletion is null)
        {
            return false;
        }

        int start = Math.Max(0, Math.Min(this.activeCompletion.ReplaceStart, this.editor.Text.Length));
        int length = Math.Max(0, Math.Min(this.activeCompletion.ReplaceLength, this.editor.Text.Length - start));

        this.suppressRefresh = true;
        try
        {
            this.editor.Select(start, length);
            this.editor.SelectedText = selected.InsertionText;
            this.editor.CaretIndex = start + selected.InsertionText.Length;
        }
        finally
        {
            this.suppressRefresh = false;
        }

        this.CloseCompletion();
        this.editor.Focus();
        return true;
    }
}

internal sealed class RehostedCompletionCatalog
{
    private readonly Dictionary<string, CompletionSymbol> globals = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CompletionEntry> keywords;

    public RehostedCompletionCatalog(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces, IEnumerable<ModelItem> variables, IEnumerable<string> keywords)
    {
        this.keywords = keywords
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static keyword => keyword, StringComparer.OrdinalIgnoreCase)
            .Select(static keyword => new CompletionEntry(keyword, keyword, CompletionKind.Keyword))
            .ToList();

        foreach (ModelItem variable in variables ?? Enumerable.Empty<ModelItem>())
        {
            string name = TryGetVariableName(variable);
            Type type = TryGetVariableType(variable);
            if (!string.IsNullOrWhiteSpace(name) && type != null && !this.globals.ContainsKey(name))
            {
                this.globals.Add(name, new CompletionSymbol(name, type, CompletionKind.Variable, isTypeSymbol: false));
            }
        }

        foreach (Type importedType in DiscoverImportedTypes(assemblies, importedNamespaces))
        {
            if (string.IsNullOrWhiteSpace(importedType.Name) || this.globals.ContainsKey(importedType.Name))
            {
                continue;
            }

            this.globals.Add(importedType.Name, new CompletionSymbol(importedType.Name, importedType, CompletionKind.Type, isTypeSymbol: true));
        }
    }

    public CompletionResponse GetCompletions(string text, int caretIndex, bool explicitRequest)
    {
        CompletionQuery query = CompletionQuery.Parse(text ?? string.Empty, caretIndex);
        IReadOnlyList<CompletionEntry> entries = query.IsMemberAccess
            ? this.GetMemberCompletions(query)
            : this.GetGlobalCompletions(query, explicitRequest);

        if (entries.Count == 0)
        {
            return null;
        }

        return new CompletionResponse(query.ReplaceStart, query.ReplaceLength, entries);
    }

    private IReadOnlyList<CompletionEntry> GetGlobalCompletions(CompletionQuery query, bool explicitRequest)
    {
        if (!explicitRequest && string.IsNullOrWhiteSpace(query.Prefix))
        {
            return Array.Empty<CompletionEntry>();
        }

        List<CompletionEntry> entries = new();
        foreach (CompletionSymbol symbol in this.globals.Values.OrderBy(static symbol => symbol.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (MatchesPrefix(symbol.Name, query.Prefix))
            {
                entries.Add(new CompletionEntry(symbol.Name, symbol.Name, symbol.Kind));
            }
        }

        foreach (CompletionEntry keyword in this.keywords)
        {
            if (MatchesPrefix(keyword.DisplayText, query.Prefix))
            {
                entries.Add(keyword);
            }
        }

        return entries
            .OrderBy(static entry => entry.Kind)
            .ThenBy(static entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();
    }

    private IReadOnlyList<CompletionEntry> GetMemberCompletions(CompletionQuery query)
    {
        if (!this.TryResolveExpressionType(query.TargetExpression, out Type targetType, out bool staticOnly))
        {
            return Array.Empty<CompletionEntry>();
        }

        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;
        bindingFlags |= staticOnly ? BindingFlags.Static : BindingFlags.Instance;

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<CompletionEntry> entries = new();

        foreach (PropertyInfo property in targetType.GetProperties(bindingFlags))
        {
            if (!property.CanRead || !seen.Add(property.Name) || !MatchesPrefix(property.Name, query.Prefix))
            {
                continue;
            }

            entries.Add(new CompletionEntry(property.Name, property.Name, CompletionKind.Property));
        }

        foreach (FieldInfo field in targetType.GetFields(bindingFlags))
        {
            if (field.IsSpecialName || !seen.Add(field.Name) || !MatchesPrefix(field.Name, query.Prefix))
            {
                continue;
            }

            entries.Add(new CompletionEntry(field.Name, field.Name, CompletionKind.Field));
        }

        foreach (MethodInfo method in targetType.GetMethods(bindingFlags))
        {
            if (method.IsSpecialName || method.DeclaringType == typeof(object) || !seen.Add(method.Name) || !MatchesPrefix(method.Name, query.Prefix))
            {
                continue;
            }

            entries.Add(new CompletionEntry(method.Name, method.Name, CompletionKind.Method));
        }

        foreach (Type nestedType in targetType.GetNestedTypes(BindingFlags.Public))
        {
            if (!seen.Add(nestedType.Name) || !MatchesPrefix(nestedType.Name, query.Prefix))
            {
                continue;
            }

            entries.Add(new CompletionEntry(nestedType.Name, nestedType.Name, CompletionKind.Type));
        }

        return entries
            .OrderBy(static entry => entry.Kind)
            .ThenBy(static entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();
    }

    private bool TryResolveExpressionType(string expression, out Type resolvedType, out bool staticOnly)
    {
        resolvedType = null;
        staticOnly = false;

        string[] parts = expression.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        if (!this.globals.TryGetValue(parts[0], out CompletionSymbol symbol))
        {
            return false;
        }

        resolvedType = symbol.SymbolType;
        staticOnly = symbol.IsTypeSymbol;

        for (int index = 1; index < parts.Length; index++)
        {
            if (!TryResolveMemberType(resolvedType, parts[index], staticOnly, out resolvedType, out staticOnly))
            {
                return false;
            }
        }

        return resolvedType != null;
    }

    private static bool TryResolveMemberType(Type sourceType, string memberName, bool staticOnly, out Type resolvedType, out bool memberIsStatic)
    {
        resolvedType = null;
        memberIsStatic = false;

        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.IgnoreCase;
        bindingFlags |= staticOnly ? BindingFlags.Static : BindingFlags.Instance;

        PropertyInfo property = sourceType.GetProperty(memberName, bindingFlags);
        if (property != null && property.CanRead)
        {
            resolvedType = property.PropertyType;
            MethodInfo accessor = property.GetGetMethod();
            memberIsStatic = accessor != null && accessor.IsStatic;
            return true;
        }

        FieldInfo field = sourceType.GetField(memberName, bindingFlags);
        if (field != null)
        {
            resolvedType = field.FieldType;
            memberIsStatic = field.IsStatic;
            return true;
        }

        MethodInfo method = sourceType.GetMethods(bindingFlags)
            .FirstOrDefault(candidate => !candidate.IsSpecialName && candidate.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase));
        if (method != null)
        {
            resolvedType = method.ReturnType;
            memberIsStatic = method.IsStatic;
            return resolvedType != null;
        }

        Type nestedType = sourceType.GetNestedTypes(BindingFlags.Public)
            .FirstOrDefault(candidate => candidate.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase));
        if (nestedType != null)
        {
            resolvedType = nestedType;
            memberIsStatic = true;
            return true;
        }

        return false;
    }

    private static IEnumerable<Type> DiscoverImportedTypes(AssemblyContextControlItem assemblies, ImportedNamespaceContextItem importedNamespaces)
    {
        HashSet<string> namespaces = new(StringComparer.OrdinalIgnoreCase);
        foreach (VisualBasicImportReference importReference in VisualBasicSettings.Default.ImportReferences)
        {
            namespaces.Add(importReference.Import);
        }

        if (importedNamespaces?.ImportedNamespaces != null)
        {
            foreach (string importedNamespace in importedNamespaces.ImportedNamespaces.Where(static item => !string.IsNullOrWhiteSpace(item)))
            {
                namespaces.Add(importedNamespace);
            }
        }

        if (namespaces.Count == 0)
        {
            return Array.Empty<Type>();
        }

        Dictionary<string, Type> discovered = new(StringComparer.OrdinalIgnoreCase);
        foreach (Assembly assembly in GetCandidateAssemblies(assemblies))
        {
            foreach (Type type in GetExportedTypes(assembly))
            {
                if (type == null || string.IsNullOrWhiteSpace(type.Namespace) || !namespaces.Contains(type.Namespace))
                {
                    continue;
                }

                if (type.IsNested || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                if (!discovered.ContainsKey(type.FullName))
                {
                    discovered.Add(type.FullName, type);
                }
            }
        }

        return discovered.Values;
    }

    private static IEnumerable<Assembly> GetCandidateAssemblies(AssemblyContextControlItem assemblies)
    {
        Dictionary<string, Assembly> loaded = new(StringComparer.OrdinalIgnoreCase);

        void AddAssembly(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
            {
                return;
            }

            string key = string.IsNullOrWhiteSpace(assembly.Location) ? assembly.FullName : assembly.Location;
            if (!string.IsNullOrWhiteSpace(key) && !loaded.ContainsKey(key))
            {
                loaded.Add(key, assembly);
            }
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddAssembly(assembly);
        }

        if (assemblies != null)
        {
            foreach (Assembly assembly in assemblies.GetEnvironmentAssemblies(null))
            {
                AddAssembly(assembly);
            }
        }

        return loaded.Values;
    }

    private static IEnumerable<Type> GetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type != null);
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static string TryGetVariableName(ModelItem modelItem)
    {
        if (modelItem?.GetCurrentValue() is LocationReference locationReference && !string.IsNullOrWhiteSpace(locationReference.Name))
        {
            return locationReference.Name;
        }

        return modelItem?.Properties["Name"]?.ComputedValue as string;
    }

    private static Type TryGetVariableType(ModelItem modelItem)
    {
        if (modelItem?.GetCurrentValue() is LocationReference locationReference)
        {
            return locationReference.Type;
        }

        return null;
    }

    private static bool MatchesPrefix(string candidate, string prefix)
        => string.IsNullOrWhiteSpace(prefix) || candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}

internal sealed class CompletionQuery
{
    private CompletionQuery(string prefix, int replaceStart, int replaceLength, bool isMemberAccess, string targetExpression)
    {
        this.Prefix = prefix;
        this.ReplaceStart = replaceStart;
        this.ReplaceLength = replaceLength;
        this.IsMemberAccess = isMemberAccess;
        this.TargetExpression = targetExpression;
    }

    public string Prefix { get; }

    public int ReplaceStart { get; }

    public int ReplaceLength { get; }

    public bool IsMemberAccess { get; }

    public string TargetExpression { get; }

    public static CompletionQuery Parse(string text, int caretIndex)
    {
        int safeCaret = Math.Max(0, Math.Min(caretIndex, text.Length));
        int replaceStart = safeCaret;
        while (replaceStart > 0 && IsIdentifierCharacter(text[replaceStart - 1]))
        {
            replaceStart--;
        }

        string prefix = text.Substring(replaceStart, safeCaret - replaceStart);
        bool isMemberAccess = replaceStart > 0 && text[replaceStart - 1] == '.';
        if (!isMemberAccess)
        {
            return new CompletionQuery(prefix, replaceStart, safeCaret - replaceStart, false, null);
        }

        int expressionEnd = replaceStart - 1;
        int expressionStart = expressionEnd;
        while (expressionStart > 0 && (IsIdentifierCharacter(text[expressionStart - 1]) || text[expressionStart - 1] == '.'))
        {
            expressionStart--;
        }

        string targetExpression = text.Substring(expressionStart, expressionEnd - expressionStart);
        return new CompletionQuery(prefix, replaceStart, safeCaret - replaceStart, true, targetExpression);
    }

    private static bool IsIdentifierCharacter(char value)
        => char.IsLetterOrDigit(value) || value == '_';
}

internal sealed class CompletionResponse
{
    public CompletionResponse(int replaceStart, int replaceLength, IReadOnlyList<CompletionEntry> entries)
    {
        this.ReplaceStart = replaceStart;
        this.ReplaceLength = replaceLength;
        this.Entries = entries ?? Array.Empty<CompletionEntry>();
    }

    public int ReplaceStart { get; }

    public int ReplaceLength { get; }

    public IReadOnlyList<CompletionEntry> Entries { get; }
}

internal sealed class CompletionEntry
{
    public CompletionEntry(string displayText, string insertionText, CompletionKind kind)
    {
        this.DisplayText = displayText;
        this.InsertionText = insertionText;
        this.Kind = kind;
    }

    public string DisplayText { get; }

    public string InsertionText { get; }

    public CompletionKind Kind { get; }
}

internal sealed class CompletionSymbol
{
    public CompletionSymbol(string name, Type symbolType, CompletionKind kind, bool isTypeSymbol)
    {
        this.Name = name;
        this.SymbolType = symbolType;
        this.Kind = kind;
        this.IsTypeSymbol = isTypeSymbol;
    }

    public string Name { get; }

    public Type SymbolType { get; }

    public CompletionKind Kind { get; }

    public bool IsTypeSymbol { get; }
}

internal enum CompletionKind
{
    Variable = 0,
    Property = 1,
    Field = 2,
    Method = 3,
    Type = 4,
    Keyword = 5,
}
