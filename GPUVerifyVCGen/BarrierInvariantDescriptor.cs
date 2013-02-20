﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Boogie;

namespace GPUVerify {
  abstract class BarrierInvariantDescriptor {

    protected Expr Predicate;
    protected Expr BarrierInvariant;
    protected QKeyValue SourceLocationInfo;
    protected KernelDualiser Dualiser;
    protected string ProcName;
    protected List<Expr> AccessExprs;

    public BarrierInvariantDescriptor(Expr Predicate, Expr BarrierInvariant,
          QKeyValue SourceLocationInfo,
          KernelDualiser Dualiser, string ProcName) {
      this.Predicate = Predicate;
      this.BarrierInvariant = BarrierInvariant;
      this.SourceLocationInfo = SourceLocationInfo;
      this.Dualiser = Dualiser;
      this.ProcName = ProcName;
      this.AccessExprs = new List<Expr>();

      if (CommandLineOptions.BarrierAccessChecks) {
        var visitor = new SubExprVisitor();
        visitor.VisitExpr(this.BarrierInvariant);
        foreach (Tuple<Expr,IdentifierExpr,Expr> pair in visitor.SubExprs) {
          var cond = pair.Item1;
          var v = pair.Item2;
          var index = pair.Item3;
          this.AccessExprs.Add(Expr.Imp(Predicate, 
            Expr.Imp(cond, BuildAccessedExpr(v.Name, index))));
        }
      }
    }

    internal abstract AssertCmd GetAssertCmd();

    internal abstract List<AssumeCmd> GetInstantiationCmds();

    protected Expr NonNegative(Expr e) {
      return Dualiser.verifier.MakeBVSge(
        e, GPUVerifier.ZeroBV());
    }

    protected Expr NotTooLarge(Expr e) {
      return Dualiser.verifier.MakeBVSlt(e,
        new IdentifierExpr(Token.NoToken, 
          Dualiser.verifier.GetGroupSize("X")));
    }

    private Expr BuildAccessedExpr(string name, Expr e) {
      return Expr.Neq(new IdentifierExpr(Token.NoToken, Dualiser.verifier.FindOrCreateNotAccessedVariable(name, e.Type)), e);
    }

    public QKeyValue GetSourceLocationInfo() {
      return SourceLocationInfo;
    }

    public List<Expr> GetAccessedExprs() {
      return AccessExprs;
    }

    class SubExprVisitor : StandardVisitor {
      internal HashSet<Tuple<Expr,IdentifierExpr,Expr>> SubExprs;
      internal List<Expr> Path;

      internal SubExprVisitor() {
        this.SubExprs = new HashSet<Tuple<Expr,IdentifierExpr,Expr>>();
        this.Path = new List<Expr>();
      }

      internal void PushPath(Expr e) {
        Path.Add(e);
      }

      internal void PopPath() {
        Path.RemoveAt(Path.Count - 1);
      }

      internal Expr BuildPathCondition() {
        return Path.Aggregate((Expr.True as Expr), (e1,e2) => Expr.And(e1, e2));
      }

      public override Expr VisitNAryExpr(NAryExpr node) {
        if (node.Fun is MapSelect) {
          Debug.Assert((node.Fun as MapSelect).Arity == 1);
          Debug.Assert(node.Args[0] is IdentifierExpr);
          IdentifierExpr v = node.Args[0] as IdentifierExpr;
          if (QKeyValue.FindBoolAttribute(v.Decl.Attributes, "group_shared") ||
              QKeyValue.FindBoolAttribute(v.Decl.Attributes, "global")) {
            Expr cond = BuildPathCondition();
            Expr index = node.Args[1];
            SubExprs.Add(new Tuple<Expr,IdentifierExpr,Expr>(cond,v,index));
          }
        } else if (node.Fun is BinaryOperator && 
                   (node.Fun as BinaryOperator).Op == BinaryOperator.Opcode.Imp) {
          var p = node.Args[0];
          var q = node.Args[1];
          PushPath(p); var e = VisitExpr(q); PopPath();
          return node; // stop recursing
        } else if (node.Fun is IfThenElse) {
          var p = node.Args[0];
          var e1 = node.Args[1];
          var e2 = node.Args[2];
          VisitExpr(p);
          PushPath(p); VisitExpr(e1); PopPath();
          PushPath(Expr.Not(p)); var e = VisitExpr(e2); PopPath();
          return node; // stop recursing
        }
        return base.VisitNAryExpr(node);
      }
    }

  }
}
