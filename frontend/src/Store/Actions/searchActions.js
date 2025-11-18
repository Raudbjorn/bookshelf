import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import getNewAuthor from 'Utilities/Author/getNewAuthor';
import monitorNewItemsOptions from 'Utilities/Author/monitorNewItemsOptions';
import monitorOptions from 'Utilities/Author/monitorOptions';
import getNewBook from 'Utilities/Book/getNewBook';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import { set, update, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';

//
// Variables

export const section = 'search';
let abortCurrentRequest = null;

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isAdding: false,
  isAdded: false,
  addError: null,
  items: [],

  // Multi-provider search settings
  searchMode: 'reconciled', // 'hardcover', 'openlibrary', 'googlebooks', 'reconciled', 'all'
  selectedProviders: ['hardcover', 'openlibrary', 'googlebooks'],

  authorDefaults: {
    rootFolderPath: '',
    monitor: monitorOptions[0].key,
    monitorNewItems: monitorNewItemsOptions[0].key,
    qualityProfileId: 0,
    metadataProfileId: 0,
    tags: []
  },

  bookDefaults: {
    rootFolderPath: '',
    monitor: monitorOptions[0].key,
    monitorNewItems: monitorNewItemsOptions[0].key,
    qualityProfileId: 0,
    metadataProfileId: 0,
    tags: []
  }
};

export const persistState = [
  'search.bookDefaults',
  'search.authorDefaults',
  'search.searchMode',
  'search.selectedProviders'
];

//
// Actions Types

export const GET_SEARCH_RESULTS = 'search/getSearchResults';
export const ADD_AUTHOR = 'search/addAuthor';
export const ADD_BOOK = 'search/addBook';
export const CLEAR_SEARCH_RESULTS = 'search/clearSearchResults';
export const SET_AUTHOR_ADD_DEFAULT = 'search/setAuthorAddDefault';
export const SET_BOOK_ADD_DEFAULT = 'search/setBookAddDefault';
export const SET_SEARCH_MODE = 'search/setSearchMode';
export const SET_SELECTED_PROVIDERS = 'search/setSelectedProviders';

//
// Action Creators

export const getSearchResults = createThunk(GET_SEARCH_RESULTS);
export const addAuthor = createThunk(ADD_AUTHOR);
export const addBook = createThunk(ADD_BOOK);
export const clearSearchResults = createAction(CLEAR_SEARCH_RESULTS);
export const setAuthorAddDefault = createAction(SET_AUTHOR_ADD_DEFAULT);
export const setBookAddDefault = createAction(SET_BOOK_ADD_DEFAULT);
export const setSearchMode = createAction(SET_SEARCH_MODE);
export const setSelectedProviders = createAction(SET_SELECTED_PROVIDERS);

//
// Action Handlers

export const actionHandlers = handleThunks({

  [GET_SEARCH_RESULTS]: function(getState, payload, dispatch) {
    dispatch(set({ section, isFetching: true }));

    if (abortCurrentRequest) {
      abortCurrentRequest();
    }

    const state = getState().search;
    const searchMode = payload.searchMode || state.searchMode;
    const selectedProviders = payload.selectedProviders || state.selectedProviders;

    // Determine the API endpoint based on search mode
    let url = '/search';
    let requestData = { term: payload.term };

    if (searchMode === 'reconciled') {
      url = '/search/provider/reconcile';
      requestData.providers = selectedProviders.join(',');
    } else if (searchMode === 'all') {
      url = '/search/provider';
      requestData.providers = selectedProviders.join(',');
    } else if (['hardcover', 'openlibrary', 'googlebooks'].includes(searchMode)) {
      url = `/search/provider/${searchMode}`;
    }

    const { request, abortRequest } = createAjaxRequest({
      url,
      data: requestData
    });

    abortCurrentRequest = abortRequest;

    request.done((data) => {
      // Transform reconciled response to match expected array format
      let items = data;
      if (searchMode === 'reconciled' && data.books) {
        // Reconciled endpoint returns {query, books: [...], authors: [...]}
        // Transform to flat array format like individual providers
        // Flatten the nested book/author object to top level
        const transformedBooks = (data.books || [])
          .filter(item => item && item.book) // Filter out null/undefined items
          .map((item, index) => {
            // Ensure required fields are present
            const foreignBookId = item.openLibraryId || item.googleBooksId || item.book?.foreignBookId || `reconciled-${index}`;
            const titleSlug = item.book?.title?.toLowerCase().replace(/[^a-z0-9]+/g, '-') || `book-${index}`;
            const author = item.book?.author || { id: 0, authorName: item.book?.authorTitle || '' };

            return {
              // Keep book nested as the component expects item.book to exist
              book: {
                ...item.book,
                // Override/ensure required fields are set AFTER spreading
                foreignBookId,
                titleSlug,
                author
              },
              id: `reconciled-book-${index}`, // Unique ID for React keys
              foreignId: foreignBookId,
              matchedProviders: item.matchedProviders,
              confidenceScore: item.confidenceScore,
              primarySource: item.primarySource,
              provider: 'reconciled'
            };
          });

        const transformedAuthors = (data.authors || [])
          .filter(item => item && item.author) // Filter out null/undefined items
          .map((item, index) => ({
            // Keep author nested as the component expects item.author to exist
            author: {
              ...item.author
            },
            id: `reconciled-author-${index}`, // Unique ID for React keys
            foreignId: item.author.foreignAuthorId || item.author.id,
            matchedProviders: item.matchedProviders,
            confidenceScore: item.confidenceScore,
            primarySource: item.primarySource,
            provider: 'reconciled'
          }));

        items = [...transformedBooks, ...transformedAuthors];
      }

      dispatch(batchActions([
        update({ section, data: items }),

        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null
        })
      ]));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });
  },

  [ADD_AUTHOR]: function(getState, payload, dispatch) {
    dispatch(set({ section, isAdding: true }));

    const foreignAuthorId = payload.foreignAuthorId;
    const items = getState().search.items;
    const itemToAdd = _.find(items, { foreignId: foreignAuthorId });
    const newAuthor = getNewAuthor(_.cloneDeep(itemToAdd.author), payload);

    const promise = createAjaxRequest({
      url: '/author',
      method: 'POST',
      dataType: 'json',
      contentType: 'application/json',
      data: JSON.stringify(newAuthor)
    }).request;

    promise.done((data) => {
      dispatch(batchActions([
        updateItem({ section: 'authors', ...data }),

        set({
          section,
          isAdding: false,
          isAdded: true,
          addError: null
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isAdding: false,
        isAdded: false,
        addError: xhr
      }));
    });
  },

  [ADD_BOOK]: function(getState, payload, dispatch) {
    dispatch(set({ section, isAdding: true }));

    const foreignBookId = payload.foreignBookId;
    const items = getState().search.items;
    const itemToAdd = _.find(items, { foreignId: foreignBookId });
    const newBook = getNewBook(_.cloneDeep(itemToAdd.book), payload);

    const promise = createAjaxRequest({
      url: '/book',
      method: 'POST',
      dataType: 'json',
      contentType: 'application/json',
      data: JSON.stringify(newBook)
    }).request;

    promise.done((data) => {
      itemToAdd.book = data;
      dispatch(batchActions([
        updateItem({ section: 'authors', ...data.author }),
        updateItem({ section: 'books', ...data }),
        updateItem({ section, ...itemToAdd }),

        set({
          section,
          isAdding: false,
          isAdded: true,
          addError: null
        }),
        set({
          section: 'books',
          isPopulated: true
        })
      ]));
    });

    promise.fail((xhr) => {
      dispatch(set({
        section,
        isAdding: false,
        isAdded: false,
        addError: xhr
      }));
    });
  }
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_AUTHOR_ADD_DEFAULT]: function(state, { payload }) {
    const newState = getSectionState(state, section);

    newState.authorDefaults = {
      ...newState.authorDefaults,
      ...payload
    };

    return updateSectionState(state, section, newState);
  },

  [SET_BOOK_ADD_DEFAULT]: function(state, { payload }) {
    const newState = getSectionState(state, section);

    newState.bookDefaults = {
      ...newState.bookDefaults,
      ...payload
    };

    return updateSectionState(state, section, newState);
  },

  [CLEAR_SEARCH_RESULTS]: function(state) {
    const {
      authorDefaults,
      bookDefaults,
      ...otherDefaultState
    } = defaultState;

    return Object.assign({}, state, otherDefaultState);
  },

  [SET_SEARCH_MODE]: function(state, { payload }) {
    const newState = getSectionState(state, section);

    newState.searchMode = payload;

    return updateSectionState(state, section, newState);
  },

  [SET_SELECTED_PROVIDERS]: function(state, { payload }) {
    const newState = getSectionState(state, section);

    newState.selectedProviders = payload;

    return updateSectionState(state, section, newState);
  }

}, defaultState, section);
