"use strict";
function safeImagePreview(bytes, media) {
  const fail = () => new Error("Immagine troppo grande, animata o non supportata per l’anteprima."), view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const need = (offset, count) => {
    if (offset < 0 || offset + count > bytes.length) throw fail();
  };
  const u16 = (offset, little = false) => {
    need(offset, 2);
    return view.getUint16(offset, little);
  }, u32 = (offset, little = false) => {
    need(offset, 4);
    return view.getUint32(offset, little);
  }, tag = (offset) => {
    need(offset, 4);
    return String.fromCharCode(...bytes.subarray(offset, offset + 4));
  }, u24 = (offset) => {
    need(offset, 3);
    return bytes[offset] + bytes[offset + 1] * 256 + bytes[offset + 2] * 65536;
  };
  function bounds(width, height, channels = 4) {
    if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 || width > 8192 || height > 8192 || width * height > 16777216 || width * height * channels > 67108864) throw fail();
    return { width, height };
  }
  if (media === "image/png") {
    need(0, 33);
    if (![137, 80, 78, 71, 13, 10, 26, 10].every((value, index) => bytes[index] === value) || tag(12) !== "IHDR" || u32(8) !== 13) throw fail();
    const depth = bytes[24], color = bytes[25];
    if (![1, 2, 4, 8, 16].includes(depth) || ![0, 2, 3, 4, 6].includes(color)) throw fail();
    const dimensions = bounds(u32(16), u32(20), Math.max(4, (color === 6 ? 4 : color === 2 ? 3 : color === 4 ? 2 : 1) * (depth === 16 ? 2 : 1)));
    let data = false, ended = false, offset = 8;
    for (let chunks = 0; offset < bytes.length && chunks < 512; chunks++) {
      const size = u32(offset), kind = tag(offset + 4);
      need(offset, 12 + size);
      if (kind === "acTL" || kind === "IHDR" && offset !== 8) throw fail();
      if (kind === "IDAT") data = true;
      offset += 12 + size;
      if (kind === "IEND") {
        if (size !== 0 || offset !== bytes.length) throw fail();
        ended = true;
        break;
      }
    }
    if (!data || !ended) throw fail();
    return dimensions;
  }
  if (media === "image/jpeg") {
    need(0, 4);
    if (bytes[0] !== 255 || bytes[1] !== 216) throw fail();
    let offset = 2, dimensions = null;
    for (let segments = 0; offset < bytes.length && segments < 512; segments++) {
      if (bytes[offset++] !== 255) throw fail();
      while (offset < bytes.length && bytes[offset] === 255) offset++;
      need(offset, 1);
      const marker = bytes[offset++];
      if (marker === 216 || marker === 217) throw fail();
      if (marker === 1 || marker >= 208 && marker <= 215) continue;
      const size = u16(offset);
      if (size < 2) throw fail();
      need(offset, size);
      if (marker === 218) {
        if (!dimensions) throw fail();
        return dimensions;
      }
      if (marker >= 192 && marker <= 207 && ![196, 200, 204].includes(marker)) {
        if (dimensions || size < 8 || bytes[offset + 2] !== 8 || ![1, 3, 4].includes(bytes[offset + 7]) || size !== 8 + bytes[offset + 7] * 3) throw fail();
        dimensions = bounds(u16(offset + 5), u16(offset + 3));
      }
      offset += size;
    }
    throw fail();
  }
  if (media === "image/webp") {
    need(0, 20);
    if (tag(0) !== "RIFF" || tag(8) !== "WEBP" || u32(4, true) + 8 !== bytes.length) throw fail();
    let offset = 12, canvas = null, image = null;
    for (let chunks = 0; offset < bytes.length && chunks < 512; chunks++) {
      const kind = tag(offset), size = u32(offset + 4, true), start = offset + 8;
      need(start, size);
      if (kind === "ANIM" || kind === "ANMF") throw fail();
      if (kind === "VP8X") {
        if (canvas || offset !== 12 || size !== 10 || bytes[start] & 2) throw fail();
        canvas = bounds(u24(start + 4) + 1, u24(start + 7) + 1);
      }
      if (kind === "VP8L") {
        if (image || size < 5 || bytes[start] !== 47) throw fail();
        const bits = u32(start + 1, true);
        if (bits >>> 29) throw fail();
        image = bounds((bits & 16383) + 1, (bits >>> 14 & 16383) + 1);
      }
      if (kind === "VP8 ") {
        if (image || size < 10 || bytes[start] & 1 || bytes[start + 3] !== 157 || bytes[start + 4] !== 1 || bytes[start + 5] !== 42) throw fail();
        image = bounds(u16(start + 6, true) & 16383, u16(start + 8, true) & 16383);
      }
      offset = start + size + (size & 1);
    }
    if (offset !== bytes.length || !image || canvas && (canvas.width !== image.width || canvas.height !== image.height)) throw fail();
    return image;
  }
  throw fail();
}
