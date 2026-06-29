#!/usr/bin/env python3
"""Convert MEL log calls that use interpolated strings into structured message
templates with named args:
  _logger.LogX($"Show {id} -> {a.Title}")  =>  _logger.LogX("Show {Id} -> {Title}", id, a.Title)
Handles the message arg whether it's arg0 (LogX) or arg1 (LogError(ex,...), Log(level,...)),
single $"..." or concatenations of "+"-joined string literals. Skips verbatim ($@) strings
(left for manual). Usage: structure_logs.py --dry|--apply <file>..."""
import sys, re

LEVELS = r'(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError|LogCritical|Log)'
CALL = re.compile(r'\b_?[a-zA-Z]\w*\.'+LEVELS+r'\(')

def find_end(s,p):
    depth=0;i=p;n=len(s)
    while i<n:
        c=s[i]
        if c=='"':
            i=_skip(s,i); continue
        if c in '$@':
            j=i;fl=set()
            while j<n and s[j] in '$@': fl.add(s[j]);j+=1
            if j<n and s[j]=='"': i=_skip(s,j,'@'in fl,'$'in fl); continue
            i+=1; continue
        if c=='(':depth+=1
        elif c==')':
            depth-=1
            if depth==0:return i
        i+=1
    return -1
def _skip(s,i,verb=False,interp=False):
    n=len(s);i+=1
    while i<n:
        c=s[i]
        if not verb and c=='\\':i+=2;continue
        if verb and c=='"' and i+1<n and s[i+1]=='"':i+=2;continue
        if interp and c=='{':
            if i+1<n and s[i+1]=='{':i+=2;continue
            i=_skiphole(s,i+1);continue
        if c=='"':return i+1
        i+=1
    return n
def _skiphole(s,i):
    d=1;n=len(s)
    while i<n:
        c=s[i]
        if c=='"':i=_skip(s,i);continue
        if c=='{':d+=1
        elif c=='}':
            d-=1
            if d==0:return i+1
        i+=1
    return n

def _pascal(t):
    t=t.strip().lstrip('@')
    return (t[:1].upper()+t[1:]) if t else 'Value'
def _name(expr):
    ids=re.findall(r'[A-Za-z_]\w*',expr)
    return _pascal(ids[-1]) if ids else 'Value'

def split_args(inner):
    args=[];d=0;cur='';i=0;n=len(inner)
    while i<n:
        c=inner[i]
        if c=='"':e=_skip(inner,i);cur+=inner[i:e];i=e;continue
        if c in '$@':
            j=i;fl=set()
            while j<n and inner[j] in '$@':fl.add(inner[j]);j+=1
            if j<n and inner[j]=='"':e=_skip(inner,j,'@'in fl,'$'in fl);cur+=inner[i:e];i=e;continue
        if c in '([{':d+=1;cur+=c;i+=1;continue
        if c in ')]}':d-=1;cur+=c;i+=1;continue
        if c==',' and d==0:args.append(cur);cur='';i+=1;continue
        cur+=c;i+=1
    if cur.strip()!='':args.append(cur)
    return args

def scan_holes(body,used):
    out=[];exprs=[];i=0;n=len(body)
    while i<n:
        c=body[i]
        if c=='{':
            if i+1<n and body[i+1]=='{':out.append('{{');i+=2;continue
            d=1;j=i+1
            while j<n and d>0:
                if body[j]=='{':d+=1
                elif body[j]=='}':
                    d-=1
                    if d==0:break
                j+=1
            h=body[i+1:j];expr=h;tail=''
            dd=0;q=0
            for k,ch in enumerate(h):
                if ch in '([{':dd+=1
                elif ch in ')]}':dd-=1
                elif dd==0 and ch=='?' and not(k+1<len(h) and h[k+1] in '.?') and not(k>0 and h[k-1]=='?'):q+=1
                elif dd==0 and ch==':':
                    if q>0:q-=1;continue
                    expr=h[:k];tail=h[k:];break
                elif dd==0 and ch==',' and q==0:expr=h[:k];tail=h[k:];break
            nm=_name(expr)
            if nm in used:used[nm]+=1;nm=f"{nm}{used[nm]}"
            else:used[nm]=1
            out.append('{'+nm+tail+'}');exprs.append(expr.strip())
            i=j+1;continue
        if c=='}' and i+1<n and body[i+1]=='}':out.append('}}');i+=2;continue
        out.append(c);i+=1
    return ''.join(out),exprs

def parse_lit(p):
    p=p.strip();m=re.match(r'^(\$@|@\$|\$|@)?"',p)
    if not m or not p.endswith('"'):return None
    pre=m.group(1) or '';body=p[len(pre)+1:-1];return pre,body

def split_plus(s):
    parts=[];d=0;cur='';i=0;n=len(s)
    while i<n:
        c=s[i]
        if c=='"':e=_skip(s,i);cur+=s[i:e];i=e;continue
        if c in '$@':
            j=i;fl=set()
            while j<n and s[j] in '$@':fl.add(s[j]);j+=1
            if j<n and s[j]=='"':e=_skip(s,j,'@'in fl,'$'in fl);cur+=s[i:e];i=e;continue
        if c in '([{':d+=1;cur+=c;i+=1;continue
        if c in ')]}':d-=1;cur+=c;i+=1;continue
        if c=='+' and d==0:parts.append(cur);cur='';i+=1;continue
        cur+=c;i+=1
    parts.append(cur);return parts

def structure_msg(arg):
    parts=split_plus(arg)
    has_lit=any(parse_lit(p) for p in parts)
    has_interp=any((parse_lit(p) and '$' in parse_lit(p)[0]) for p in parts)
    if not has_lit:return None
    if not has_interp and len(parts)==1:return None  # single plain literal: nothing to optimize
    template='';exprs=[];used={}
    for p in parts:
        lit=parse_lit(p)
        if lit:
            pre,body=lit
            if '@' in pre:return None  # verbatim: skip
            if '$' in pre:
                t,e=scan_holes(body,used);template+=t;exprs+=e
            else:
                template+=body
        else:
            nm=_name(p)
            if nm in used:used[nm]+=1;nm=f"{nm}{used[nm]}"
            else:used[nm]=1
            template+='{'+nm+'}';exprs.append(p.strip())
    return '"'+template+'"',exprs

def process(s):
    out=[];i=0;cnt=0
    while True:
        m=CALL.search(s,i)
        if not m:out.append(s[i:]);break
        op=m.end()-1;end=find_end(s,op)
        if end<0:out.append(s[i:m.end()]);i=m.end();continue
        inner=s[op+1:end];args=split_args(inner)
        # the message arg is the first arg structure_msg can transform
        midx=None;res=None
        for k,a in enumerate(args):
            r=structure_msg(a)
            if r is not None:midx=k;res=r;break
        if midx is None:out.append(s[i:end+1]);i=end+1;continue
        newmsg,exprs=res
        newargs=args[:midx]+[' '+newmsg]+args[midx+1:]+[' '+e for e in exprs]
        out.append(s[i:op+1]);out.append(','.join(a.strip() if j==0 else ' '+a.strip() for j,a in enumerate([newmsg]+ ([] )) ) if False else '')
        # rebuild simply:
        rebuilt=newmsg+''.join(', '+e for e in exprs)
        # preserve other args (before/after midx)
        before=[a.strip() for a in args[:midx]]
        after=[a.strip() for a in args[midx+1:]]
        allargs=before+[rebuilt]+after
        out[-1]=''  # discard placeholder
        out.append(', '.join(allargs)); out.append(')')
        i=end+1;cnt+=1
    return ''.join(out),cnt

def main():
    mode=sys.argv[1];files=sys.argv[2:]
    for f in files:
        s=open(f).read();ns,c=process(s)
        if mode=='--dry':
            print(f"=== {f}: {c} structured ===")
            for line in ns.split('\n'):
                if re.search(r'\.'+LEVELS+r'\("',line): print("  "+line.strip()[:160])
        else:
            open(f,'w').write(ns);print(f"OK {f} ({c})")
if __name__=='__main__':main()
