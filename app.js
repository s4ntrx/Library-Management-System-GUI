const state = { books: [], members: [], filter: 'all', query: '', loading: true, error: null };
const $ = (selector) => document.querySelector(selector);

// Builds DOM nodes with textContent only, so book titles typed by users can never run as HTML.
function el(tag, props = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(props)) {
    if (key === 'class') node.className = value;
    else if (key.startsWith('on')) node.addEventListener(key.slice(2), value);
    else if (value === true) node.setAttribute(key, '');
    else if (value !== false && value != null) node.setAttribute(key, value);
  }
  node.append(...children.filter((child) => child != null && child !== false));
  return node;
}

async function api(path, { method = 'GET', body } = {}) {
  const response = await fetch(`/api${path}`, {
    method,
    headers: body ? { 'Content-Type': 'application/json' } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  const data = await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.error ?? `The server answered with an error (${response.status}).`);
  return data;
}

let toastTimer;
function toast(message) {
  const box = $('#toast');
  box.textContent = message;
  box.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => box.classList.remove('show'), 4500);
}

let latestRequest = 0;
async function refresh({ quiet = false } = {}) {
  const requestId = ++latestRequest;
  if (!quiet) {
    state.loading = true;
    state.error = null;
    render();
  }
  try {
    const query = state.query ? `?query=${encodeURIComponent(state.query)}` : '';
    const [books, members] = await Promise.all([api(`/books${query}`), api('/members')]);
    if (requestId !== latestRequest) return;
    state.books = books;
    state.members = members;
    state.error = null;
  } catch (error) {
    if (requestId !== latestRequest) return;
    state.error = error.message;
  }
  state.loading = false;
  render();
}

function render() {
  renderStats();
  renderCatalogue();
  renderMembers();
}

function renderStats() {
  const stats = $('#stats');
  if (state.loading || state.error) { stats.textContent = ''; return; }
  if (state.query) { stats.textContent = `${state.books.length} matching “${state.query}”`; return; }
  const onLoan = state.books.filter((b) => b.status === 'Borrowed').length;
  const overdue = state.books.filter((b) => b.daysOverdue > 0).length;
  stats.textContent = `${state.books.length} titles, ${onLoan} on loan, ${overdue} overdue, ${state.members.length} members`;
}

function skeletonRow(columns) {
  return el('tr', { 'aria-hidden': 'true' },
    ...Array.from({ length: columns }, () => el('td', {}, el('span', { class: 'bar' }))));
}

function showNote(box, message, kind) {
  box.hidden = false;
  box.dataset.kind = kind;
  box.replaceChildren(el('p', { role: kind === 'error' ? 'alert' : null }, message));
  if (kind === 'error') box.append(el('button', { class: 'btn small', type: 'button', onclick: () => refresh() }, 'Try again'));
}

function visibleBooks() {
  return state.books.filter((book) => {
    if (state.filter === 'available') return book.status === 'Available';
    if (state.filter === 'borrowed') return book.status === 'Borrowed';
    if (state.filter === 'overdue') return book.daysOverdue > 0;
    return true;
  });
}

function emptyMessage() {
  if (state.query) return `Nothing matches “${state.query}”.`;
  if (state.filter !== 'all') return 'No books in this view.';
  return 'The catalogue is empty. Add the first book to begin.';
}

function statusCell(book) {
  if (book.daysOverdue > 0) return el('span', { class: 'status late' }, `Overdue ${book.daysOverdue} d`);
  if (book.status === 'Borrowed') return el('span', { class: 'status loan' }, 'On loan');
  return el('span', { class: 'status free' }, 'Available');
}

function bookRow(book) {
  const action = book.status === 'Borrowed'
    ? el('button', { class: 'btn small', type: 'button', onclick: () => returnBook(book) }, 'Return')
    : el('button', { class: 'btn small primary', type: 'button', onclick: () => openBorrow(book) }, 'Borrow');
  return el('tr', {},
    el('td', { class: 'num' }, String(book.id)),
    el('td', {}, el('span', { class: 'title' }, book.title), el('span', { class: 'sub' }, book.author)),
    el('td', { class: 'isbn' }, book.isbn),
    el('td', {}, statusCell(book)),
    el('td', { class: 'due' }, book.dueOn ?? '', book.borrowedByName ? el('span', { class: 'sub' }, book.borrowedByName) : null),
    el('td', { class: 'act' }, action));
}

function renderCatalogue() {
  const rows = $('#book-rows');
  const note = $('#catalogue-note');
  rows.replaceChildren();
  note.hidden = true;
  if (state.loading) { rows.append(...Array.from({ length: 5 }, () => skeletonRow(6))); return; }
  if (state.error) { showNote(note, `Could not load the library. ${state.error}`, 'error'); return; }
  const books = visibleBooks();
  if (books.length === 0) { showNote(note, emptyMessage(), 'empty'); return; }
  rows.append(...books.map(bookRow));
}

function renderMembers() {
  const rows = $('#member-rows');
  const note = $('#members-note');
  rows.replaceChildren();
  note.hidden = true;
  if (state.loading) { rows.append(...Array.from({ length: 3 }, () => skeletonRow(4))); return; }
  if (state.error) { showNote(note, `Could not load the library. ${state.error}`, 'error'); return; }
  if (state.members.length === 0) { showNote(note, 'No members yet. Add the first member so books can be lent.', 'empty'); return; }
  rows.append(...state.members.map((member) => el('tr', {},
    el('td', { class: 'num' }, String(member.id)),
    el('td', {}, el('span', { class: 'title' }, member.name)),
    el('td', {}, member.email),
    el('td', {}, String(member.activeLoans)))));
}

function openDialog(dialog) {
  dialog.querySelector('form').reset();
  dialog.querySelector('.form-error').textContent = '';
  dialog.showModal();
  dialog.querySelector('input, select')?.focus();
}

let borrowTarget = null;
function openBorrow(book) {
  borrowTarget = book;
  const none = state.members.length === 0;
  $('#borrow-title').textContent = `Lend “${book.title}”`;
  $('#borrow-member').replaceChildren(...state.members.map((member) =>
    el('option', { value: member.id }, `${member.name} (${member.activeLoans} out)`)));
  $('#borrow-member').disabled = none;
  $('#borrow-submit').disabled = none;
  $('#borrow-none').hidden = !none;
  $('#borrow-dialog .form-error').textContent = '';
  $('#borrow-dialog').showModal();
}

async function returnBook(book) {
  try {
    const result = await api(`/books/${book.id}/return`, { method: 'POST' });
    toast(result.daysOverdue > 0
      ? `“${book.title}” returned, ${result.daysOverdue} days overdue.`
      : `“${book.title}” returned.`);
  } catch (error) {
    toast(error.message);
  }
  await refresh({ quiet: true });
}

function handleForm(selector, submit) {
  const form = $(selector);
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    const button = form.querySelector('button[type="submit"]');
    const error = form.querySelector('.form-error');
    error.textContent = '';
    button.disabled = true;
    try {
      await submit(Object.fromEntries(new FormData(form)));
      form.closest('dialog').close();
    } catch (problem) {
      error.textContent = problem.message;
    } finally {
      button.disabled = false;
    }
  });
}

handleForm('#book-form', async (values) => {
  const book = await api('/books', { method: 'POST', body: { title: values.title, author: values.author, isbn: values.isbn } });
  toast(`Saved “${book.title}” as number ${book.id}.`);
  await refresh({ quiet: true });
});

handleForm('#member-form', async (values) => {
  const member = await api('/members', { method: 'POST', body: { name: values.name, email: values.email } });
  toast(`Registered ${member.name} as member ${member.id}.`);
  await refresh({ quiet: true });
});

handleForm('#borrow-form', async (values) => {
  const book = await api(`/books/${borrowTarget.id}/borrow`, { method: 'POST', body: { memberId: Number(values.memberId) } });
  toast(`“${book.title}” is due ${book.dueOn}.`);
  await refresh({ quiet: true });
});

let searchTimer;
$('#search').addEventListener('input', (event) => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => { state.query = event.target.value.trim(); refresh({ quiet: true }); }, 200);
});

document.querySelectorAll('input[name="filter"]').forEach((radio) =>
  radio.addEventListener('change', () => { state.filter = radio.value; renderCatalogue(); }));
document.querySelectorAll('[data-close]').forEach((button) =>
  button.addEventListener('click', () => button.closest('dialog').close()));
$('#add-book').addEventListener('click', () => openDialog($('#book-dialog')));
$('#add-member').addEventListener('click', () => openDialog($('#member-dialog')));

function route() {
  const target = location.hash === '#members' ? '#members' : '#catalogue';
  $('#catalogue').hidden = target !== '#catalogue';
  $('#members').hidden = target !== '#members';
  document.querySelectorAll('nav a').forEach((link) => {
    if (link.hash === target) link.setAttribute('aria-current', 'page');
    else link.removeAttribute('aria-current');
  });
}

window.addEventListener('hashchange', route);
route();
refresh();
