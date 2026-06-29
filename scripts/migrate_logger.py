#!/usr/bin/env python3
"""Rewrite legacy static NoMercy `Logger.<Category>(...)` calls to MEL ILogger
calls. String/char/verbatim/interpolation-aware paren matching so parens inside
interpolated strings don't confuse call-boundary detection.

Usage:
  migrate_logger.py --dry <expr> <file>...     # report only, no writes
  migrate_logger.py --apply <expr> <file>...   # rewrite in place
<expr> is the logger expression to call, e.g. 'logger' or '_logger'.
Management-API members (LogEmitted/GetLogs/SetLogLevel/LogTypes/LogType/
WriteBanner/GetColor/Write) are NOT in the category set, so they're left alone.
"""
import sys, re

DEFAULT_LEVEL = {
 'App':'Information','Setup':'Information','System':'Information','Auth':'Information',
 'Register':'Information','Certificate':'Information','Configuration':'Information','Access':'Information',
 'Encoder':'Information','Ripper':'Information','Http':'Information','Ping':'Information','Socket':'Information',
 'AcoustId':'Information','AniDb':'Information','AudioDb':'Information','CoverArt':'Information','FanArt':'Information',
 'Fingerprint':'Information','MovieDb':'Information','MusicBrainz':'Information','Tvdb':'Information',
 'OpenSubs':'Information','MusixMatch':'Information','Lrclib':'Information','Newtonsoft':'Information',
 'Queue':'Debug','Request':'Debug',
 'Debug':'Debug','Info':'Information','Warning':'Warning','Error':'Error','Verbose':'Verbose',
}
MEL={'Verbose':'LogTrace','Debug':'LogDebug','Information':'LogInformation','Warning':'LogWarning','Error':'LogError','Fatal':'LogCritical','Critical':'LogCritical'}

def skip_str(s,i,quote,verbatim=False,interp=False):
    n=len(s); i+=1
    while i<n:
        c=s[i]
        if not verbatim and c=='\\': i+=2; continue
        if verbatim and c==quote and i+1<n and s[i+1]==quote: i+=2; continue
        if interp and c=='{':
            if i+1<n and s[i+1]=='{': i+=2; continue
            i=skip_hole(s,i+1); continue
        if c==quote: return i+1
        i+=1
    return n

def skip_hole(s,i):
    depth=1; n=len(s)
    while i<n:
        c=s[i]
        if c=='"' or c=="'": i=skip_str(s,i,c); continue
        if c in '$@':
            j=i; fl=set()
            while j<n and s[j] in '$@': fl.add(s[j]); j+=1
            if j<n and s[j]=='"': i=skip_str(s,j,'"',verbatim=('@' in fl),interp=('$' in fl)); continue
            i+=1; continue
        if c=='{': depth+=1
        elif c=='}':
            depth-=1
            if depth==0: return i+1
        i+=1
    return n

def find_call_end(s,p):
    depth=0; i=p; n=len(s)
    while i<n:
        c=s[i]
        if c=='"' or c=="'": i=skip_str(s,i,c); continue
        if c in '$@':
            j=i; fl=set()
            while j<n and s[j] in '$@': fl.add(s[j]); j+=1
            if j<n and s[j]=='"': i=skip_str(s,j,'"',verbatim=('@' in fl),interp=('$' in fl)); continue
            i+=1; continue
        if c=='/' and i+1<n and s[i+1]=='/':
            while i<n and s[i]!='\n': i+=1
            continue
        if c=='/' and i+1<n and s[i+1]=='*':
            i+=2
            while i+1<n and not(s[i]=='*' and s[i+1]=='/'): i+=1
            i+=2; continue
        if c=='(': depth+=1
        elif c==')':
            depth-=1
            if depth==0: return i
        i+=1
    return -1

CATPAT=re.compile(r'\bLogger\.(' + '|'.join(sorted(DEFAULT_LEVEL,key=len,reverse=True)) + r')\b')

def rewrite(s, expr, report):
    out=[]; i=0; count=0; flags=[]; skipped=[]
    while True:
        m=CATPAT.search(s,i)
        if not m: out.append(s[i:]); break
        cat=m.group(1); j=m.end()
        while j<len(s) and s[j] in ' \t\r\n': j+=1
        if j>=len(s) or s[j]!='(':
            out.append(s[i:m.end()]); i=m.end(); continue
        end=find_call_end(s,j)
        if end<0:
            out.append(s[i:m.end()]); i=m.end(); continue
        inner=s[j+1:end]
        mm=re.search(r',\s*LogEventLevel\.(\w+)\s*$', inner)
        if mm: lvl=mm.group(1); newinner=inner[:mm.start()]
        else: lvl=DEFAULT_LEVEL[cat]; newinner=inner
        mel=MEL.get(lvl,'LogInformation')
        ident = re.fullmatch(r'\s*[A-Za-z_]\w*\s*', newinner) is not None
        out.append(s[i:m.start()])
        if ident and not report:
            out.append(s[m.start():end+1])  # skip: leave legacy call for manual fix
            if report is False: skipped.append(f"  SKIP Logger.{cat}({newinner.strip()}) -> needs manual {mel}")
        else:
            out.append(f'{expr}.{mel}('+newinner+')'); count+=1
        if report:
            tag='[IDENT-ARG SKIP]' if ident else ''
            flags.append(f"  Logger.{cat} -> {expr}.{mel} {'(explicit '+lvl+')' if mm else '(default)'} {tag} :: {newinner.strip()[:70]!r}")
        i=end+1
    return ''.join(out), count, flags+skipped

def main():
    mode=sys.argv[1]; expr=sys.argv[2]; files=sys.argv[3:]
    for f in files:
        s=open(f).read()
        ns,c,flags=rewrite(s,expr,mode=='--dry')
        print(f"=== {f}: {c} calls ===")
        if mode=='--dry':
            for fl in flags: print(fl)
        else:
            open(f,'w').write(ns); print("  written")
if __name__=='__main__':
    main()
