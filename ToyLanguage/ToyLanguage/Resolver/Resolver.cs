﻿using System;
using System.Collections.Generic;
using System.Linq;

using ToyLanguage.Interfaces;
using ToyLanguage.Lexer;
using ToyLanguage.Parser.Expressions;
using ToyLanguage.Parser.Statements;
using ToyLanguage.Resolver.Exceptions;

namespace ToyLanguage.Resolver
{
    internal enum ScopeTypes
    {
        NONE,
        FUNCTION,
        METHOD,
        CLASS_CONSTRUCTOR,
        SUB_CLASS_METHOD
    }

    internal class Resolver : IExpressionVisitor<object>, IStatementVisitor<object>
    {
        private ScopeTypes currentScopeType = ScopeTypes.NONE;

        // TODO and remove [Obsolete]
        // constructor(private interpreter: Interpreter) {}

        private readonly Stack<Dictionary<string, bool>> scopes = new Stack<Dictionary<string, bool>>();

        private void BeginScope() => scopes.Push(new Dictionary<string, bool>());

        private void EndScope() => scopes.Pop();

        private Dictionary<string, bool> GetCurrentScope() => scopes.Peek();

        private void Declare(Token token)
        {
            if (scopes.Count == 0)
                return;

            Dictionary<string, bool> currentScope = GetCurrentScope();

            if (currentScope.ContainsKey(token.Lexeme))
            {
                Console.WriteLine($"Redeclaring variable { token.Lexeme}");
            }
            else
                currentScope[token.Lexeme] = false;
        }

        private void Define(Token token)
        {
            if (scopes.Count == 0)
                return;

            GetCurrentScope()[token.Lexeme] = true;
        }

        [Obsolete]
        private void ResolveLocal(IExpression expression, Token token)
        {
            int scopesCount = scopes.Count - 1;

            for (int i = scopesCount; i > -1; --i)
            {
                if (scopes.ElementAt(i).ContainsKey(token.Lexeme))
                {
                    //this.interpreter.resolve(expression, scopesCount - i);
                    return;
                }
            }
        }

        private void ResolveFunction(Function statement, ScopeTypes scopeType)
        {
            ScopeTypes parentScopeType = currentScopeType;
            currentScopeType = scopeType;

            BeginScope();

            statement.Parameters.ForEach(param =>
            {
                Declare(param);
                Define(param);
            });

            statement.Body.ForEach(stmt => stmt.Accept(this));

            EndScope();

            currentScopeType = parentScopeType;
        }

        public Resolver Resolve(IStatement[] statements)
        {
            statements.ToList().ForEach(statement => statement.Accept(this));

            return this;
        }

        public object VisitBlockStatement(Block statement)
        {
            BeginScope();

            statement.Statements.ForEach(stmt => stmt.Accept(this));

            EndScope();

            return null;
        }

        public object VisitLetStatement(Let statement)
        {
            Declare(statement.Name);

            if (statement.Initializer != null)
                statement.Initializer.Accept(this);

            Define(statement.Name);

            return null;
        }

        public object VisitConstStatement(Const statement)
        {
            Declare(statement.Name);
            statement.Initializer.Accept(this);
            Define(statement.Name);

            return null;
        }

        [Obsolete]
        public object VisitVariableExpression(Variable expression)
        {
            if (scopes.Count > 0 && GetCurrentScope()[expression.Name.Lexeme] == false)
                throw new InvalidInitializerException(expression.Name.Lexeme);

            ResolveLocal(expression, expression.Name);

            return null;
        }

        [Obsolete]
        public object VisitAssignExpression(Assign expression)
        {
            expression.Value.Accept(this);
            ResolveLocal(expression, expression.Name);

            return null;
        }

        public object VisitFunctionStatement(Function statement)
        {
            Declare(statement.Name);
            Define(statement.Name);
            ResolveFunction(statement, ScopeTypes.FUNCTION);

            return null;
        }

        public object VisitExpressionStatement(Expression statement)
        {
            statement.CurrentExpression.Accept(this);

            return null;
        }

        public object VisitIfStatement(If statement)
        {
            statement.Condition.Accept(this);
            statement.ThenBranch.Accept(this);

            if (statement.ElseBranch != null)
            {
                statement.ElseBranch.Accept(this);
            }

            return null;
        }

        public object VisitPrintStatement(Print statement)
        {
            statement.Expression.Accept(this);

            return null;
        }

        public object VisitReturnStatement(Return statement)
        {
            if (currentScopeType == ScopeTypes.NONE)
                throw new UnexpectedTokenException(statement.Keyword.Lexeme);

            if (currentScopeType == ScopeTypes.CLASS_CONSTRUCTOR)
                Console.WriteLine("Invalid <return> statement from class constructor");

            if (statement.Value != null)
                statement.Value.Accept(this);

            return null;
        }

        public object VisitWhileStatement(While statement)
        {
            statement.Condition.Accept(this);
            statement.Body.Accept(this);

            return null;
        }

        public object VisitClassStatement(Class statement)
        {
            ScopeTypes parentScopeType = currentScopeType;
            currentScopeType = ScopeTypes.METHOD;

            if (statement.Name.Lexeme == "constructor")
                currentScopeType = ScopeTypes.CLASS_CONSTRUCTOR;

            Define(statement.Name);
            Declare(statement.Name);

            BeginScope();

            Dictionary<string, bool> currentScope = GetCurrentScope();
            currentScope["this"] = true;

            if (statement.Superclass != null)
            {
                currentScope["super"] = true;
                currentScopeType = ScopeTypes.SUB_CLASS_METHOD;

                string className = statement.Name.Lexeme;

                string superClassName = statement.Superclass.Name.Lexeme;

                if (className == superClassName)
                    Console.WriteLine($"Class { className} can't extend {superClassName}");

                statement.Superclass.Accept(this);
            }

            statement.Methods.ForEach(method => ResolveFunction(method, currentScopeType));

            EndScope();

            currentScopeType = parentScopeType;

            return null;
        }

        public object VisitBinaryExpression(Binary expression)
        {
            expression.Left.Accept(this);
            expression.Right.Accept(this);

            return null;
        }

        public object VisitCallExpression(Call expression)
        {
            expression.Callee.Accept(this);

            expression.Args.ForEach(arg => arg.Accept(this));

            return null;
        }

        public object VisitAccessObjectPropertyExpression(AccessObjectProperty expression)
        {
            expression.Object.Accept(this);

            return null;
        }

        public object VisitGroupingExpression(Grouping expression)
        {
            expression.Expression.Accept(this);

            return null;
        }

        public object VisitLogicalExpression(Logical expression)
        {
            expression.Left.Accept(this);
            expression.Right.Accept(this);

            return null;
        }

        public object VisitSetObjectPropertyExpression(SetObjectProperty expression)
        {
            expression.Value.Accept(this);
            expression.Object.Accept(this);

            return null;
        }

        [Obsolete]
        public object VisitThisExpression(This expression)
        {
            if (currentScopeType != ScopeTypes.METHOD && currentScopeType != ScopeTypes.SUB_CLASS_METHOD)
            {
                Console.WriteLine("Invalid use of <this> outside of class instance");
                return null;
            }
            ResolveLocal(expression, expression.Name);

            return null;
        }

        [Obsolete]
        public object VisitSuperExpression(Super expression)
        {
            if (currentScopeType != ScopeTypes.SUB_CLASS_METHOD)
            {
                Console.WriteLine("Invalid use of <super> outside of class sub class instance");
                return null;
            }
            ResolveLocal(expression, expression.Name);

            return null;
        }

        public object VisitUnaryExpression(Unary expression)
        {
            expression.Right.Accept(this);

            return null;
        }

        public object VisitLiteralExpression(Literal expression) => throw new NotImplementedException();
    }
}