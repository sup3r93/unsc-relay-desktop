from pathlib import Path
import subprocess
paths=subprocess.check_output(['git','ls-files','-z'],text=True).split('\0')
errors=[]
for name in paths:
 if name and Path(name).name.lower().startswith('readme') and Path(name).suffix.lower() in {'.md','.txt',''}:
  value=Path(name).read_text(encoding='utf-8-sig')
  if '## Italiano' not in value or '## English' not in value or value.index('## English')>value.index('## Italiano'):errors.append(name)
if errors:raise SystemExit('README requires English first, followed by Italian: '+', '.join(errors))
print('All project README files include English first, followed by Italian.')
