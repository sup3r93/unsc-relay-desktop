"use strict";
function installTerminalOutputBatcher(term, shouldBatch) {
  const write = term.write.bind(term), writeln = term.writeln.bind(term), dispose = term.dispose.bind(term);
  let jobs = [], bytes = 0, timer = null, closed = false;
  const maximumBytes = 256 * 1024, maximumJobs = 128;
  function flush() {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
    if (closed || !jobs.length) return;
    const batch = jobs;
    jobs = [];
    bytes = 0;
    for (let i = 0; i < batch.length; i++) {
      const job = batch[i];
      if (job.data instanceof Uint8Array && !job.newline && !job.callback) {
        let end = i + 1, length = job.data.length;
        while (end < batch.length && batch[end].data instanceof Uint8Array && !batch[end].newline && !batch[end].callback) {
          length += batch[end].data.length;
          end++;
        }
        if (end > i + 1) {
          const combined = new Uint8Array(length);
          let offset = 0;
          for (let j = i; j < end; j++) {
            combined.set(batch[j].data, offset);
            offset += batch[j].data.length;
          }
          write(combined);
          i = end - 1;
          continue;
        }
      }
      (job.newline ? writeln : write)(job.data, job.callback);
    }
  }
  function enqueue(data, callback, newline) {
    const original = newline ? writeln : write;
    if (closed) return original(data, callback);
    if (!(typeof data === "string" || data instanceof Uint8Array) || !shouldBatch()) {
      flush();
      return original(data, callback);
    }
    const length = typeof data === "string" ? data.length * 2 : data.length;
    if (length >= maximumBytes) {
      flush();
      return original(data, callback);
    }
    if (bytes + length > maximumBytes || jobs.length >= maximumJobs) flush();
    jobs.push({ data: data instanceof Uint8Array ? data.slice() : data, callback, newline });
    bytes += length;
    if (timer === null) timer = setTimeout(flush, 250);
  }
  term.write = (data, callback) => enqueue(data, callback, false);
  term.writeln = (data, callback) => enqueue(data, callback, true);
  term.dispose = () => {
    if (closed) return;
    flush();
    closed = true;
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
    jobs = [];
    bytes = 0;
    dispose();
  };
  return { flush };
}
