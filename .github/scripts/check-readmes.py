from pathlib import Path
import subprocess
paths=subprocess.check_output(['git','ls-files','-z'],text=True).split('\0')
errors=[]
for name in paths:
 if name and Path(name).name.lower().startswith('readme') and Path(name).suffix.lower() in {'.md','.txt',''}:
  value=Path(name).read_text(encoding='utf-8-sig')
  if '## Italiano' not in value or '## English' not in value:errors.append(name)
if errors:raise SystemExit('Missing Italian/English README sections: '+', '.join(errors))
print('All project README files include Italian and English.')
