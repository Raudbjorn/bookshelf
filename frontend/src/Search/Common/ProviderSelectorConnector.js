import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { setSearchMode } from 'Store/Actions/searchActions';
import ProviderSelector from './ProviderSelector';

function createMapStateToProps() {
  return createSelector(
    (state) => state.search.searchMode,
    (searchMode) => {
      return {
        searchMode
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    onSearchModeChange({ value }) {
      dispatch(setSearchMode(value));
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(ProviderSelector);
