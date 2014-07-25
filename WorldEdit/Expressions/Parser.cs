﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;

namespace WorldEdit.Expressions
{
	public static class Parser
	{
		public static Expression ParseExpression(IEnumerable<Token> postfix)
		{
			var stack = new Stack<Expression>();
			foreach (var token in postfix)
			{
				switch (token.Type)
				{
					case Token.TokenType.BinaryOperator:
						switch ((Token.OperatorType)token.Value)
						{
							case Token.OperatorType.And:
								stack.Push(new AndExpression(stack.Pop(), stack.Pop()));
								continue;
							case Token.OperatorType.Or:
								stack.Push(new OrExpression(stack.Pop(), stack.Pop()));
								continue;
							case Token.OperatorType.Xor:
								stack.Push(new XorExpression(stack.Pop(), stack.Pop()));
								continue;
							default:
								return null;
						}
					case Token.TokenType.Test:
						stack.Push(new TestExpression((Test)token.Value));
						continue;
					case Token.TokenType.UnaryOperator:
						switch ((Token.OperatorType)token.Value)
						{
							case Token.OperatorType.Not:
								stack.Push(new NotExpression(stack.Pop()));
								continue;
							default:
								return null;
						}
					default:
						return null;
				}
			}
			return stack.Pop();
		}
		public static List<Token> ParseInfix(string str)
		{
			str = str.Replace(" ", "").ToLower();
			var tokens = new List<Token>();

			for (int i = 0; i < str.Length; i++)
			{
				switch (str[i])
				{
					case '&':
						if (str[i + 1] == '&')
							i++;
						tokens.Add(new Token { Type = Token.TokenType.BinaryOperator, Value = Token.OperatorType.And });
						continue;
					case '!':
						tokens.Add(new Token { Type = Token.TokenType.UnaryOperator, Value = Token.OperatorType.Not });
						continue;
					case '|':
						if (str[i + 1] == '|')
							i++;
						tokens.Add(new Token { Type = Token.TokenType.BinaryOperator, Value = Token.OperatorType.Or });
						continue;
					case '^':
						tokens.Add(new Token { Type = Token.TokenType.BinaryOperator, Value = Token.OperatorType.Xor });
						continue;
					case '(':
						tokens.Add(new Token { Type = Token.TokenType.OpenParentheses });
						continue;
					case ')':
						tokens.Add(new Token { Type = Token.TokenType.CloseParentheses });
						continue;
				}

				var test = new StringBuilder();
				while (i < str.Length && (Char.IsLetterOrDigit(str[i]) || str[i] == '!' || str[i] == '='))
					test.Append(str[i++]);
				i--;

				string[] expression = test.ToString().Split('=');
				string lhs = expression[0];
				string rhs = "";
				bool negated = false;

				if (expression.Length > 1)
				{
					if (lhs[lhs.Length - 1] == '!')
					{
						lhs = lhs.Substring(0, lhs.Length - 1);
						negated = true;
					}
					rhs = expression[1];
				}
				tokens.Add(new Token { Type = Token.TokenType.Test, Value = ParseTest(lhs, rhs, negated) });
			}
			return tokens;
		}
		public static List<Token> ParsePostfix(IEnumerable<Token> infix)
		{
			var postfix = new List<Token>();
			var stack = new Stack<Token>();

			foreach (var token in infix)
			{
				switch (token.Type)
				{
					case Token.TokenType.BinaryOperator:
					case Token.TokenType.OpenParentheses:
					case Token.TokenType.UnaryOperator:
						stack.Push(token);
						break;
					case Token.TokenType.CloseParentheses:
						while (stack.Peek().Type != Token.TokenType.OpenParentheses)
							postfix.Add(stack.Pop());
						stack.Pop();

						if (stack.Count > 0 && stack.Peek().Type == Token.TokenType.UnaryOperator)
							postfix.Add(stack.Pop());
						break;
					case Token.TokenType.Test:
						postfix.Add(token);
						break;
				}
			}

			while (stack.Count > 0)
				postfix.Add(stack.Pop());
			return postfix;
		}
		public static Test ParseTest(string lhs, string rhs, bool negated)
		{
			Test test;
			switch (lhs)
			{
				case "honey":
					return test = t => t.liquid > 0 && t.liquidType() == 2;
				case "lava":
					return test = t => t.liquid > 0 && t.liquidType() == 1;
				case "liquid":
					return test = t => t.liquid > 0;
				case "tile":
					if (String.IsNullOrEmpty(rhs))
						return test = t => t.active();

					List<int> tiles = Tools.GetTileID(rhs);
					if (tiles.Count == 0)
						throw new ArgumentException("No tile matched.");
					if (tiles.Count > 1)
						throw new ArgumentException("More than one tile matched.");
					return test = t => (t.active() && t.type == tiles[0]) != negated;
				case "tilepaint":
					{
						if (String.IsNullOrEmpty(rhs))
							return test = t => t.active() && t.color() != 0;

						List<int> colors = Tools.GetColorID(rhs);
						if (colors.Count == 0)
							throw new ArgumentException("No color matched.");
						if (colors.Count > 1)
							throw new ArgumentException("More than one color matched.");
						return test = t => (t.active() && t.color() == colors[0]) != negated;
					}
				case "wall":
					if (String.IsNullOrEmpty(rhs))
						return test = t => t.wall != 0;

					List<int> walls = Tools.GetTileID(rhs);
					if (walls.Count == 0)
						throw new ArgumentException("No wall matched.");
					if (walls.Count > 1)
						throw new ArgumentException("More than one wall matched.");
					return test = t => (t.wall == walls[0]) != negated;
				case "wallpaint":
					{
						if (String.IsNullOrEmpty(rhs))
							return test = t => t.wall > 0 && t.wallColor() != 0;

						List<int> colors = Tools.GetColorID(rhs);
						if (colors.Count == 0)
							throw new ArgumentException("No color matched.");
						if (colors.Count > 1)
							throw new ArgumentException("More than one color matched.");
						return test = t => (t.wall > 0 && t.wallColor() == colors[0]) != negated;
					}
				case "water":
					return test = t => t.liquid > 0 && t.liquidType() == 0;
				case "wire":
					return test = t => t.wire();
				case "wire2":
					return test = t => t.wire2();
				case "wire3":
					return test = t => t.wire3();
				default:
					throw new ArgumentException("Invalid test.");
			}
		}
		public static bool TryParseTree(IEnumerable<string> parameters, out Expression expression)
		{
			expression = null;
			if (parameters.FirstOrDefault() != "=>")
				return false;

			try
			{
				expression = ParseExpression(ParsePostfix(ParseInfix(String.Join(" ", parameters.Skip(1)))));
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}