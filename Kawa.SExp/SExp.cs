using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Kawa.SExp
{
	public class Cons
	{
		public object Car { get; set; }
		public object OCdr { get; set; }
		public Cons Cdr { get { return (OCdr != null) ? OCdr as Cons : null; } set { OCdr = value; } }
		public Cons() { }
		public Cons(object car)
		{
			Car = car;
		}
		public Cons(object car, object cdr)
		{
			Car = car;
			OCdr = cdr;
		}
		public override string ToString()
		{
			var stringHelper = new Func<object, object>(i =>
			{
				if (i is string)
					return '"' + (string)i + '"';
				return i;
			});
			var ret = new StringBuilder();
			ret.Append('(');
			ret.Append(stringHelper(Car));
			if (!(OCdr is Cons))
			{
				ret.Append(" . ");
				ret.Append(OCdr == null ? "nil" : stringHelper(OCdr));
				ret.Append(')');
				return ret.ToString();
			}
			var c = Cdr;
			while (c != null)
			{
				ret.Append(' ');
				ret.Append(stringHelper(c.Car));
				c = c.Cdr;
			}
			ret.Append(')');
			return ret.ToString();
			/*
			if (Car is string)
			{
				var s = Car as string;
				if (s.Contains(' ') || s.Contains('\t') || s.Contains('\n'))
					return string.Format("(\"{0}\" . {1})", s, Cdr ?? "nil");
			}
			return string.Format("({0} . {1})", Car ?? "nil", Cdr ?? "nil");
			*/
		}
	}

	public class Symbol
	{
		public Dictionary<string, object> Properties { get; set; }
		public string Value { get { return (string)Properties["print-name"]; } }
		public Symbol(string printName)
		{
			Properties = new Dictionary<string, object>() { { "print-name", printName } };
		}
		public override string ToString()
		{
			return Value;
		}
		public static implicit operator string(Symbol s)
		{
			return s.Value;
		}
		public static implicit operator Symbol(string s)
		{
			return new Symbol(s);
		}
		public override bool Equals(object obj)
		{
			if (obj is string)
				return Value == (string)obj;
			if (obj is Symbol)
				return Value == ((Symbol)obj).Value;
			return base.Equals(obj);
		}
		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}
	}

	public class SExpression
	{
		public string Original { get; private set; }
		public object Data { get; private set; }
		public SExpression() { }
		public SExpression(string str)
		{
			if (str.StartsWith("\xEF\xBB\xBF"))
				str = str.Substring(3);
			Original = str;
			Data = Parse(str, false);
		}
		public SExpression(string str, bool toCons)
		{
			if (str.StartsWith("\xEF\xBB\xBF"))
				str = str.Substring(3);
			Original = str;
			Data = Parse(str, toCons);
		}
		public SExpression(byte[] data)
		{
			Original = new string(data.Select(x => (char)x).ToArray());
			Data = Parse(data, false);
		}
		public SExpression(byte[] data, bool toCons)
		{
			Original = new string(data.Select(x => (char)x).ToArray());
			Data = Parse(data, toCons);
		}
		public SExpression(Stream stream)
		{
			var pos = stream.Position;
			var bom = new byte[3];
			stream.Read(bom, 0, 3);
			if (!(bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF))
				stream.Seek(pos, SeekOrigin.Begin);
			Original = "[stream]";
			Data = Parse(stream, false);
		}
		public SExpression(Stream stream, bool toCons)
		{
			var pos = stream.Position;
			var bom = new byte[3];
			stream.Read(bom, 0, 3);
			if (!(bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF))
				stream.Seek(pos, SeekOrigin.Begin);
			Original = "[stream]";
			Data = Parse(stream, toCons);
		}
		public override string ToString()
		{
			return ToSexpression(Data);
		}
		public string ToSexpression()
		{
			return ToSexpression(Data);
		}
		public string ToSexpression(object thing)
		{
			if (thing is string)
			{
				if (((string)thing).Contains(' ') || ((string)thing).Contains('\t') || ((string)thing).Contains('\n'))
					return string.Format("\"{0}\"", thing);
				else
					return thing.ToString();
			}
			if (thing is List<object>)
			{
				return string.Format("({0})", string.Join(" ", ((List<object>)thing).Select(x => ToSexpression(x))));
			}
			return thing.ToString();
		}
		private enum States { TokenStart, ReadQuotedString, ReadStringOrNumber, Binary, Comment }
		private int lastInt;
		private object Parse(string str, bool toCons = false)
		{
			var state = States.TokenStart;
			var tokens = new List<object>();
			var word = new StringBuilder();
			var binBlock = new List<byte>();
			foreach (var ch in str)
			{
				switch (state)
				{
					case States.Comment:
						if (ch == '\r')
							state = States.TokenStart;
						break;
					case States.TokenStart:
						if (ch == ';')
						{
							state = States.Comment;
						}
						else if (ch == '(' || ch == ')' || ch == '\'')
						{
							tokens.Add(ch);
						}
						else if (char.IsWhiteSpace(ch))
						{
							//just eat it.
						}
						else if (ch >= 255 && lastInt > 0)
						{
							throw new Exception("No binary blobs in string mode, please.");
						}
						else if (ch == '\"')
						{
							state = States.ReadQuotedString;
							word.Clear();
						}
						else
						{
							state = States.ReadStringOrNumber;
							word.Clear();
							word.Append(ch);
						}
						break;
					case States.ReadQuotedString:
						if (ch == '\"')
						{
							tokens.Add(word.ToString());
							state = States.TokenStart;
						}
						else
						{
							word.Append(ch);
						}
						break;
					case States.ReadStringOrNumber:
						if (char.IsWhiteSpace(ch))
						{
							tokens.Add(SymbolOrNumber(word.ToString()));
							state = States.TokenStart;
						}
						else if (ch == ')')
						{
							tokens.Add(SymbolOrNumber(word.ToString()));
							tokens.Add(')');
							state = States.TokenStart;
						}
						else
						{
							word.Append(ch);
						}
						break;
				}
			}
			var i = 0;
			if (toCons)
				return TokensToConses((List<object>)TokensToArray(tokens, ref i));
			return TokensToArray(tokens, ref i);
		}
		private object Parse(byte[] data, bool toCons = false)
		{
			var state = States.TokenStart;
			var tokens = new List<object>();
			var word = new StringBuilder();
			var binBlock = new List<byte>();
			foreach (var b in data)
			{
				var ch = (char)b;
				switch (state)
				{
					case States.Comment:
						if (ch == '\r')
							state = States.TokenStart;
						break;
					case States.TokenStart:
						if (ch == ';')
						{
							state = States.Comment;
						}
						else if (ch == '(' || ch == ')' || ch == '\'')
						{
							tokens.Add(ch);
						}
						else if (char.IsWhiteSpace(ch))
						{
							//just eat it.
						}
						else if (b == 255 && lastInt > 0)
						{
							state = States.Binary;
							binBlock.Clear();
						}
						else if (ch == '\"')
						{
							state = States.ReadQuotedString;
							word.Clear();
						}
						else
						{
							state = States.ReadStringOrNumber;
							word.Clear();
							word.Append(ch);
						}
						break;
					case States.ReadQuotedString:
						if (ch == '\"')
						{
							tokens.Add(word.ToString());
							state = States.TokenStart;
						}
						else
						{
							word.Append(ch);
						}
						break;
					case States.ReadStringOrNumber:
						if (char.IsWhiteSpace(ch))
						{
							tokens.Add(SymbolOrNumber(word.ToString()));
							state = States.TokenStart;
						}
						else if (ch == ')')
						{
							tokens.Add(SymbolOrNumber(word.ToString()));
							tokens.Add(')');
							state = States.TokenStart;
						}
						else
						{
							word.Append(ch);
						}
						break;
					case States.Binary:
						if (lastInt > 0)
						{
							binBlock.Add(b);
							lastInt--;
						}
						if (lastInt == 0)
						{
							tokens.Add(binBlock.ToArray());
							state = States.TokenStart;
						}
						break;
				}
			}
			var i = 0;
			if (toCons)
				return TokensToConses((List<object>)TokensToArray(tokens, ref i));
			return TokensToArray(tokens, ref i);
		}
		private object Parse(Stream stream, bool toCons = false)
		{
			var state = States.TokenStart;
			var tokens = new List<object>();
			var word = new StringBuilder();
			var binBlock = new List<byte>();
			//foreach (var b in stream)
			var d = 0;
			while ((d = stream.ReadByte()) != -1)
			{
				var b = (byte)d;
				var ch = (char)b;
				switch (state)
				{
					case States.Comment:
						if (ch == '\r')
							state = States.TokenStart;
						break;
					case States.TokenStart:
						if (ch == ';')
						{
							state = States.Comment;
						}
						else if (ch == '(' || ch == ')' || ch == '\'')
						{
							tokens.Add(ch);
						}
						else if (char.IsWhiteSpace(ch))
						{
							//just eat it.
						}
						else if (b == 255 && lastInt > 0)
						{
							state = States.Binary;
							binBlock.Clear();
						}
						else if (ch == '\"')
						{
							state = States.ReadQuotedString;
							word.Clear();
						}
						else
						{
							state = States.ReadStringOrNumber;
							word.Clear();
							word.Append(ch);
						}
						break;
					case States.ReadQuotedString:
						if (ch == '\"')
						{
							tokens.Add(word.ToString());
							state = States.TokenStart;
						}
						else
						{
							word.Append(ch);
						}
						break;
					case States.ReadStringOrNumber:
						if (char.IsWhiteSpace(ch))
						{
							tokens.Add(SymbolOrNumber(word.ToString()));
							state = States.TokenStart;
						}
						else if (ch == ')')
						{
							tokens.Add(SymbolOrNumber(word.ToString()));
							tokens.Add(')');
							state = States.TokenStart;
						}
						else
						{
							word.Append(ch);
						}
						break;
					case States.Binary:
						if (lastInt > 0)
						{
							binBlock.Add(b);
							lastInt--;
						}
						if (lastInt == 0)
						{
							tokens.Add(binBlock.ToArray());
							state = States.TokenStart;
						}
						break;
				}
			}
			var i = 0;
			if (toCons)
				return TokensToConses((List<object>)TokensToArray(tokens, ref i));
			return TokensToArray(tokens, ref i);
		}

		private object SymbolOrNumber(string word)
		{
			var i = 0;
			var f = 0.0f;
			if (int.TryParse(word, out i))
			{
				lastInt = i;
				return i;
			}
			else if (float.TryParse(word, System.Globalization.NumberStyles.Float, null, out f))
				return f;
			else if (float.TryParse(word.Replace(",", "."), System.Globalization.NumberStyles.Float, null, out f))
				return f;
			else
				return new Symbol(word);
		}

		private Cons TokensToConses(List<object> tokens)
		{
			var i = 0;
			var list = TokensToArray(tokens, ref i);
			var cons = new Cons();
			var ret = cons;
			for (var j = 0; j < tokens.Count; j++)
			{
				cons.Car = tokens[j];
				if (cons.Car is List<object>)
					cons.Car = TokensToConses((List<object>)cons.Car);
				cons.Cdr = null;
				if (j < tokens.Count - 1)
					cons.Cdr = new Cons(tokens[j + 1]);
				if (cons.Cdr != null)
					cons = cons.Cdr;
			}
			return ret;
		}

		private object TokensToArray(List<object> tokens, ref int index)
		{
			var result = new List<object>();
			while (index < tokens.Count)
			{
				if (tokens[index] is char && (char)tokens[index] == '\'')
				{
					index++;
					var quote = new List<object>();
					quote.Add(new Symbol("quote"));
					if (tokens[index] is char && (char)tokens[index] == '(')
					{
						index++;
						quote.Add(TokensToArray(tokens, ref index));
					}
					else
						quote.Add(tokens[index]); //quote.Add(new List<object>() { tokens[index] });
					result.Add(quote);
				}
				else if (tokens[index] is char && (char)tokens[index] == '(')
				{
					index++;
					result.Add(TokensToArray(tokens, ref index));
				}
				else if (tokens[index] is char && (char)tokens[index] == ')')
				{
					return result;
				}
				else
				{
					result.Add(tokens[index]);
				}
				index++;
			}
			return result;
		}
	}

	public class SConsExpression : SExpression
	{
		public new string Original { get; private set; }
		public new Cons Data { get; private set; }
		public SConsExpression(string str)
		{
			var foo = new SExpression(str, true);
			this.Data = foo.Data as Cons;
			this.Original = foo.Original;
		}
		public SConsExpression()
		{
		}
	}
}
