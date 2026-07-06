using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChessGUI.Core.Analysis;
using ChessGUI.Core.Game;

namespace ChessGUI.App.ViewModels;

/// <summary>
/// Oyun ağacını, tıklanabilir jetonlardan oluşan düz bir listeye çevirir (hamle numaraları,
/// hamleler, parantezli varyantlar ve yorumlar). Tahta her değiştiğinde yeniden kurulur.
/// </summary>
public sealed partial class MoveListViewModel : ObservableObject
{
    private readonly BoardViewModel _board;

    public ObservableCollection<MoveToken> Tokens { get; } = new();

    public MoveListViewModel(BoardViewModel board)
    {
        _board = board;
        _board.Changed += (_, _) => Rebuild();
        Rebuild();
    }

    [RelayCommand]
    private void Navigate(MoveToken? token)
    {
        if (token?.Node != null) _board.GoTo(token.Node);
    }

    private void Rebuild()
    {
        Tokens.Clear();
        BuildMoves(_board.Tree.Root, forceNumber: true, depth: 0);
    }

    private void BuildMoves(GameNode node, bool forceNumber, int depth)
    {
        if (!node.HasChildren) return;

        GameNode main = node.Children[0];
        AddMoveTokens(main, forceNumber, depth);

        bool hasVariations = node.Children.Count > 1;
        for (int i = 1; i < node.Children.Count; i++)
        {
            int vd = depth + 1;
            Add(new MoveToken { Text = "(", Depth = vd });
            AddMoveTokens(node.Children[i], forceNumber: true, vd);
            BuildMoves(node.Children[i], forceNumber: false, vd);
            Add(new MoveToken { Text = ")", Depth = vd });
        }

        bool forceNext = hasVariations || !string.IsNullOrEmpty(main.Comment) || main.Nags.Count > 0;
        BuildMoves(main, forceNext, depth);
    }

    private void AddMoveTokens(GameNode node, bool forceNumber, int depth)
    {
        if (node.WhiteMoved)
            Add(new MoveToken { Text = $"{node.MoveNumber}.", Depth = depth });
        else if (forceNumber)
            Add(new MoveToken { Text = $"{node.MoveNumber}...", Depth = depth });

        string text = node.San + NagGlyphs.ForNags(node.Nags);
        int severity = 0;
        if (node.Quality is { } q)
        {
            text += MoveClassifier.Symbol(q);
            severity = MoveClassifier.Severity(q);
        }
        Add(new MoveToken
        {
            Node = node,
            Text = text,
            IsMove = true,
            IsCurrent = node == _board.CurrentNode,
            Depth = depth,
            Severity = severity
        });

        if (!string.IsNullOrEmpty(node.Comment))
            Add(new MoveToken { Text = node.Comment!, IsComment = true, Depth = depth });
    }

    private void Add(MoveToken token) => Tokens.Add(token);
}
