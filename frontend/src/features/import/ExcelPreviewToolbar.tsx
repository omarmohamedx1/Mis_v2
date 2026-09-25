import { Trash2, Undo2 } from 'lucide-react';
import { Button } from '../../components/common/Button';
import { importCopy } from './importCopy';

type Props = {
  arabic: boolean;
  excludedCount: number;
  onExclude: () => void;
  onRestore: () => void;
  readyCount: number;
  selectedCount: number;
};

export function ExcelPreviewToolbar({ arabic, excludedCount, onExclude, onRestore, readyCount, selectedCount }: Props) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-blue-200 bg-blue-50 px-4 py-3 text-blue-950">
      <p className="text-sm font-semibold">{importCopy.previewReady(arabic, readyCount)}</p>
      <div className="flex flex-wrap gap-2">
        <Button disabled={selectedCount === 0} fullWidth={false} leftIcon={<Trash2 className="h-4 w-4" />} onClick={onExclude} size="sm" type="button" variant="outline">
          {importCopy.excludeSelected(arabic)}
        </Button>
        <Button disabled={excludedCount === 0} fullWidth={false} leftIcon={<Undo2 className="h-4 w-4" />} onClick={onRestore} size="sm" type="button" variant="outline">
          {importCopy.includeSelected(arabic)}
        </Button>
      </div>
    </div>
  );
}
